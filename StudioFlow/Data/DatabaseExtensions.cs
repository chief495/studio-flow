using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

public static class DatabaseExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider s)
    {
        using var scope = s.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        await db.Database.MigrateAsync();
        var tenant = DemoStudio.Id;
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var password = config["Seed:AdminPassword"];
        var demoPassword = config["Seed:DemoClientPassword"];
        if (
            !string.IsNullOrWhiteSpace(demoPassword)
            && !await db.Users.AnyAsync(user => user.Email == "client@studioflow.local")
        )
        {
            db.Users.Add(
                new User
                {
                    Email = "client@studioflow.local",
                    PasswordHash = PasswordHasher.Hash(demoPassword),
                    Role = Role.Client,
                    TenantId = tenant,
                }
            );
        }
        if (
            !await db.Users.AnyAsync(x => x.Role == Role.Admin)
            && !string.IsNullOrWhiteSpace(password)
        )
        {
            db.Users.Add(
                new User
                {
                    Email = "admin@studioflow.local",
                    PasswordHash = PasswordHasher.Hash(password),
                    Role = Role.Admin,
                    TenantId = tenant,
                }
            );
        }
        if (!await db.Rooms.AnyAsync())
        {
            var tariff = new Tariff
            {
                Name = "Стандарт",
                HourlyRate = 1000,
                TenantId = tenant,
            };
            db.Tariffs.Add(tariff);
            db.Rooms.Add(
                new Room
                {
                    Name = "Репетиционная комната",
                    Capacity = 6,
                    TenantId = tenant,
                    Tariff = tariff,
                }
            );
        }
        await db.SaveChangesAsync();
    }
}
