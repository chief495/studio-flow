using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class DatabaseExtensions {
 public static async Task InitializeDatabaseAsync(this IServiceProvider s) {
 using var scope = s.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
 await db.Database.EnsureCreatedAsync();
 await db.Database.ExecuteSqlRawAsync("""
 CREATE EXTENSION IF NOT EXISTS btree_gist;
 DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'booking_no_overlap') THEN
 ALTER TABLE "Bookings" ADD CONSTRAINT booking_no_overlap EXCLUDE USING gist ("RoomId" WITH =, tstzrange("StartsAt", "EndsAt", '[)') WITH &&) WHERE ("Status" = 0);
 END IF;
 END $$;
 """);
 var tenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
 var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
 var password = config["Seed:AdminPassword"];
 if (!await db.Users.AnyAsync(x => x.Role == Role.Admin) && !string.IsNullOrWhiteSpace(password)) {
 db.Users.Add(new User { Email = "admin@studioflow.local", PasswordHash = PasswordHasher.Hash(password), Role = Role.Admin, TenantId = tenant });
 }
 if (!await db.Rooms.AnyAsync()) {
 var tariff = new Tariff { Name = "Стандарт", HourlyRate = 1000, TenantId = tenant };
 db.Tariffs.Add(tariff); db.Rooms.Add(new Room { Name = "Репетиционная комната", Capacity = 6, TenantId = tenant, Tariff = tariff });
 }
 await db.SaveChangesAsync();
 }
}
