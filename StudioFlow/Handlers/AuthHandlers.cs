using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class AuthHandlers
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/auth/register", Register).WithTags("Auth");

        app.MapPost("/api/auth/login", Login).WithTags("Auth");
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        StudioFlowDbContext db,
        IValidator<RegisterRequest> validator,
        IConfiguration configuration
    )
    {
        var normalized = request with { Email = request.Email?.Trim().ToLowerInvariant() ?? "" };
        await validator.ValidateAndThrowAsync(normalized);

        if (await db.Users.AnyAsync(user => user.Email == normalized.Email))
        {
            return Results.Problem(statusCode: 409, title: "Этот email уже зарегистрирован.");
        }

        var user = new User
        {
            Email = normalized.Email,
            PasswordHash = PasswordHasher.Hash(normalized.Password),
            Role = Role.Client,
            TenantId = DemoStudio.Id,
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: "IX_Users_Email"
                    }
            )
        {
            return Results.Problem(statusCode: 409, title: "Этот email уже зарегистрирован.");
        }

        return Results.Json(
            new
            {
                token = TokenService.TokenFor(user, configuration.GetSection("Jwt")),
                user.Id,
                user.Email,
                role = user.Role.ToString(),
                user.TenantId,
            },
            statusCode: StatusCodes.Status201Created
        );
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        StudioFlowDbContext db,
        IConfiguration configuration
    )
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.Unauthorized();
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(item => item.Email == email);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            new
            {
                token = TokenService.TokenFor(user, configuration.GetSection("Jwt")),
                user.Id,
                user.Email,
                role = user.Role.ToString(),
                user.TenantId,
            }
        );
    }
}
