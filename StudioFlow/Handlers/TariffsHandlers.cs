using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public static class TariffsHandlers
{
    public static void Map(WebApplication app)
    {
        var tariffs = app.MapGroup("/api/tariffs").RequireAuthorization();
        tariffs.MapGet("", ListTariffs);
        tariffs
            .MapPost("", CreateTariff)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
        tariffs
            .MapPut("/{id:guid}", UpdateTariff)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
    }

    private static async Task<IResult> ListTariffs(
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        CancellationToken cancellationToken
    )
    {
        var tenantId = user.TenantId();
        var tariffs = await db
            .Tariffs.AsNoTracking()
            .Where(tariff => tariff.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return Results.Ok(tariffs);
    }

    private static async Task<IResult> CreateTariff(
        TariffEdit request,
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        IValidator<TariffEdit> validator,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var tariff = new Tariff
        {
            Name = request.Name,
            HourlyRate = request.HourlyRate,
            TenantId = user.TenantId(),
        };
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(tariff);
    }

    private static async Task<IResult> UpdateTariff(
        Guid id,
        TariffEdit request,
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        IValidator<TariffEdit> validator,
        CancellationToken cancellationToken
    )
    {
        var tenantId = user.TenantId();
        var tariff =
            await db.Tariffs.SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId,
                cancellationToken
            ) ?? throw new ResourceNotFoundException("Тариф не найден.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        tariff.Name = request.Name;
        tariff.HourlyRate = request.HourlyRate;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
