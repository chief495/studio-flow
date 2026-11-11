using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class AuthHandlers
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest r, StudioFlowDbContext db, IValidator<RegisterRequest> v) => { var vr = await v.ValidateAsync(r); if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary()); var email = r.Email.ToLowerInvariant(); if (await db.Users.AnyAsync(x => x.Email == email)) return Results.Conflict("Email is already registered."); var u = new User { Email = email, PasswordHash = PasswordHasher.Hash(r.Password), TenantId = r.TenantId }; db.Users.Add(u); await db.SaveChangesAsync(); return Results.Ok(new { token = TokenService.TokenFor(u, app.Configuration.GetSection("Jwt")), u.Id, u.Email, role = u.Role.ToString(), u.TenantId }); }).WithTags("Auth");

        app.MapPost("/api/auth/login", async (LoginRequest r, StudioFlowDbContext db) => { var u = await db.Users.SingleOrDefaultAsync(x => x.Email == r.Email.ToLowerInvariant()); if (u is null || !PasswordHasher.Verify(r.Password, u.PasswordHash)) return Results.Unauthorized(); return Results.Ok(new { token = TokenService.TokenFor(u, app.Configuration.GetSection("Jwt")), u.Id, u.Email, role = u.Role.ToString(), u.TenantId }); }).WithTags("Auth");

    }
}
