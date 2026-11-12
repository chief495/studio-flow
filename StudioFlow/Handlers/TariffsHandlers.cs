using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

public static class TariffsHandlers
{
    public static void Map(WebApplication app)
    {
        app.MapGet(
                "/api/tariffs",
                async (ClaimsPrincipal p, StudioFlowDbContext db) =>
                    await db.Tariffs.Where(x => x.TenantId == p.TenantId()).ToListAsync()
            )
            .RequireAuthorization();

        app.MapPost(
                "/api/tariffs",
                async (TariffEdit r, ClaimsPrincipal p, StudioFlowDbContext db) =>
                {
                    if (string.IsNullOrWhiteSpace(r.Name) || r.HourlyRate <= 0)
                        return Results.BadRequest();
                    var t = new Tariff
                    {
                        Name = r.Name,
                        HourlyRate = r.HourlyRate,
                        TenantId = p.TenantId(),
                    };
                    db.Tariffs.Add(t);
                    await db.SaveChangesAsync();
                    return Results.Ok(t);
                }
            )
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));

        app.MapPut(
                "/api/tariffs/{id:guid}",
                async (Guid id, TariffEdit r, ClaimsPrincipal p, StudioFlowDbContext db) =>
                {
                    var t = await db.Tariffs.SingleOrDefaultAsync(x =>
                        x.Id == id && x.TenantId == p.TenantId()
                    );
                    if (t is null)
                        return Results.NotFound();
                    if (string.IsNullOrWhiteSpace(r.Name) || r.HourlyRate <= 0)
                        return Results.BadRequest();
                    t.Name = r.Name;
                    t.HourlyRate = r.HourlyRate;
                    await db.SaveChangesAsync();
                    return Results.NoContent();
                }
            )
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
    }
}
