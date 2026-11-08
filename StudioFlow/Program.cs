using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StudioFlow.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, config) => config.WriteTo.Console());
builder.Services.AddDbContext<StudioFlowDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddSingleton<IValidator<RegisterRequest>, RegisterValidator>();
builder.Services.AddSingleton<IValidator<RoomRequest>, RoomValidator>();
builder.Services.AddSingleton<IValidator<BookingRequest>, BookingValidator>();
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)) });
builder.Services.AddAuthorization(); builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
var app = builder.Build(); await app.Services.InitializeDatabaseAsync(); app.UseSerilogRequestLogging(); app.UseSwagger(); app.UseSwaggerUI(); app.UseAuthentication(); app.UseAuthorization();
app.UseDefaultFiles(); app.UseStaticFiles();
app.Use(async (context, next) => { try { await next(context); } catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23P01" }) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync("Это время уже занято."); } });
app.MapGet("/api/studio", () => new { id = Guid.Parse("00000000-0000-0000-0000-000000000001"), name = "StudioFlow" });
app.MapGet("/api/rooms/{id:guid}/slots", async (Guid id, DateTime date, ClaimsPrincipal p, StudioFlowDbContext db) => {
    if (!await db.Rooms.AnyAsync(x => x.Id == id && x.TenantId == p.TenantId() && x.IsActive)) return Results.NotFound();
    var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    var occupied = await db.Bookings.Where(x => x.RoomId == id && x.Status == BookingStatus.Active && x.EndsAt > start && x.StartsAt < start.AddDays(1)).ToListAsync();
    return Results.Ok(Enumerable.Range(9, 13).Select(h => new { startsAt = start.AddHours(h), endsAt = start.AddHours(h + 1), available = start.AddHours(h) > DateTime.UtcNow && !occupied.Any(b => b.StartsAt < start.AddHours(h + 1) && b.EndsAt > start.AddHours(h)) }));
}).RequireAuthorization();
app.MapPut("/api/rooms/{id:guid}", async (Guid id, RoomEdit r, ClaimsPrincipal p, StudioFlowDbContext db) => {
    var room = await db.Rooms.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == p.TenantId());
    if (room is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(r.Name) || r.Capacity < 1 || (r.TariffId.HasValue && !await db.Tariffs.AnyAsync(t => t.Id == r.TariffId && t.TenantId == p.TenantId()))) return Results.BadRequest();
    room.Name = r.Name; room.Capacity = r.Capacity; room.IsActive = r.IsActive; room.TariffId = r.TariffId;
    await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
app.MapGet("/api/tariffs", async (ClaimsPrincipal p, StudioFlowDbContext db) => await db.Tariffs.Where(x => x.TenantId == p.TenantId()).ToListAsync()).RequireAuthorization();
app.MapPost("/api/tariffs", async (TariffEdit r, ClaimsPrincipal p, StudioFlowDbContext db) => {
    if (string.IsNullOrWhiteSpace(r.Name) || r.HourlyRate <= 0) return Results.BadRequest();
    var t = new Tariff { Name = r.Name, HourlyRate = r.HourlyRate, TenantId = p.TenantId() }; db.Tariffs.Add(t); await db.SaveChangesAsync(); return Results.Ok(t);
}).RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
app.MapPut("/api/tariffs/{id:guid}", async (Guid id, TariffEdit r, ClaimsPrincipal p, StudioFlowDbContext db) => {
    var t = await db.Tariffs.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == p.TenantId()); if (t is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(r.Name) || r.HourlyRate <= 0) return Results.BadRequest(); t.Name = r.Name; t.HourlyRate = r.HourlyRate; await db.SaveChangesAsync(); return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/auth/register", async (RegisterRequest r, StudioFlowDbContext db, IValidator<RegisterRequest> v) => { var vr = await v.ValidateAsync(r); if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary()); var email = r.Email.ToLowerInvariant(); if (await db.Users.AnyAsync(x => x.Email == email)) return Results.Conflict("Email is already registered."); var u = new User { Email = email, PasswordHash = PasswordHasher.Hash(r.Password), TenantId = r.TenantId }; db.Users.Add(u); await db.SaveChangesAsync(); return Results.Ok(new { token = TokenFor(u, jwt), u.Id, u.Email, role = u.Role.ToString(), u.TenantId }); }).WithTags("Auth");
app.MapPost("/api/auth/login", async (LoginRequest r, StudioFlowDbContext db) => { var u = await db.Users.SingleOrDefaultAsync(x => x.Email == r.Email.ToLowerInvariant()); if (u is null || !PasswordHasher.Verify(r.Password, u.PasswordHash)) return Results.Unauthorized(); return Results.Ok(new { token = TokenFor(u, jwt), u.Id, u.Email, role = u.Role.ToString(), u.TenantId }); }).WithTags("Auth");
app.MapGet("/api/rooms", async (ClaimsPrincipal p, StudioFlowDbContext db) => Results.Ok(await db.Rooms.Where(x => x.TenantId == p.TenantId() && x.IsActive).Include(x => x.Tariff).ToListAsync())).RequireAuthorization();
app.MapPost("/api/rooms", async (RoomRequest r, ClaimsPrincipal p, StudioFlowDbContext db, IValidator<RoomRequest> v) => { if (!p.IsInRole(Role.Admin.ToString()) && !p.IsInRole(Role.Manager.ToString())) return Results.Forbid(); var vr = await v.ValidateAsync(r); if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary()); var room = new Room { Name = r.Name, Capacity = r.Capacity, TenantId = p.TenantId() }; db.Rooms.Add(room); await db.SaveChangesAsync(); return Results.Created($"/api/rooms/{room.Id}", room); }).RequireAuthorization();
app.MapGet("/api/bookings", async (ClaimsPrincipal p, StudioFlowDbContext db) => Results.Ok(await db.Bookings.Where(x => x.TenantId == p.TenantId()).Include(x => x.Room).OrderBy(x => x.StartsAt).ToListAsync())).RequireAuthorization();
app.MapPost("/api/bookings", async (BookingRequest r, ClaimsPrincipal p, StudioFlowDbContext db, IValidator<BookingRequest> v) => { var vr = await v.ValidateAsync(r); if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary()); var tenant = p.TenantId(); if (!await db.Rooms.AnyAsync(x => x.Id == r.RoomId && x.TenantId == tenant && x.IsActive)) return Results.NotFound("Room was not found."); if (await db.Bookings.AnyAsync(x => x.RoomId == r.RoomId && x.TenantId == tenant && x.Status == BookingStatus.Active && r.StartsAt < x.EndsAt && r.EndsAt > x.StartsAt)) return Results.Conflict("The room is already booked for this time range."); var b = new Booking { RoomId = r.RoomId, UserId = p.UserId(), TenantId = tenant, StartsAt = r.StartsAt.ToUniversalTime(), EndsAt = r.EndsAt.ToUniversalTime() }; db.Bookings.Add(b); await db.SaveChangesAsync(); return Results.Created($"/api/bookings/{b.Id}", b); }).RequireAuthorization();
app.MapPost("/api/bookings/{id:guid}/cancel", async (Guid id, ClaimsPrincipal p, StudioFlowDbContext db) => { var b = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == p.TenantId()); if (b is null) return Results.NotFound(); if (b.UserId != p.UserId() && !p.IsInRole(Role.Admin.ToString()) && !p.IsInRole(Role.Manager.ToString())) return Results.Forbid(); b.Status = BookingStatus.Cancelled; await db.SaveChangesAsync(); return Results.Ok(b); }).RequireAuthorization();
await app.RunAsync();
static string TokenFor(User u, IConfiguration j) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(j["Issuer"], j["Audience"], new[] { new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Role, u.Role.ToString()), new Claim("tenant_id", u.TenantId.ToString()) }, expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(j["Key"]!)), SecurityAlgorithms.HmacSha256)));

namespace StudioFlow.Models
{
    public class User { public Guid Id { get; set; } = Guid.NewGuid(); public required string Email { get; set; } public required string PasswordHash { get; set; } public Role Role { get; set; } public Guid TenantId { get; set; } }
}

public record RegisterRequest(string Email, string Password, Guid TenantId); public record LoginRequest(string Email, string Password); public record RoomRequest(string Name, int Capacity); public record BookingRequest(Guid RoomId, DateTime StartsAt, DateTime EndsAt);
public enum Role { Admin, Manager, Client } public enum BookingStatus { Active, Cancelled }
public class RegisterValidator : AbstractValidator<RegisterRequest> { public RegisterValidator() { RuleFor(x => x.Email).EmailAddress(); RuleFor(x => x.Password).MinimumLength(8); RuleFor(x => x.TenantId).NotEmpty(); } } public class RoomValidator : AbstractValidator<RoomRequest> { public RoomValidator() { RuleFor(x => x.Name).NotEmpty().MaximumLength(120); RuleFor(x => x.Capacity).InclusiveBetween(1, 100); } } public class BookingValidator : AbstractValidator<BookingRequest> { public BookingValidator() { RuleFor(x => x.RoomId).NotEmpty(); RuleFor(x => x).Must(x => x.EndsAt > x.StartsAt).WithMessage("EndsAt must be after StartsAt."); } }

public class Room { public Guid Id { get; set; } = Guid.NewGuid(); public required string Name { get; set; } public int Capacity { get; set; } public bool IsActive { get; set; } = true; public Guid TenantId { get; set; } public Guid? TariffId { get; set; } public Tariff? Tariff { get; set; } } public class Tariff { public Guid Id { get; set; } = Guid.NewGuid(); public required string Name { get; set; } public decimal HourlyRate { get; set; } public Guid TenantId { get; set; } } public class Booking { public Guid Id { get; set; } = Guid.NewGuid(); public Guid RoomId { get; set; } public Room? Room { get; set; } public Guid UserId { get; set; } public Guid TenantId { get; set; } public DateTime StartsAt { get; set; } public DateTime EndsAt { get; set; } public BookingStatus Status { get; set; } }
public class StudioFlowDbContext(DbContextOptions<StudioFlowDbContext> o) : DbContext(o) { public DbSet<User> Users => Set<User>(); public DbSet<Room> Rooms => Set<Room>(); public DbSet<Tariff> Tariffs => Set<Tariff>(); public DbSet<Booking> Bookings => Set<Booking>(); protected override void OnModelCreating(ModelBuilder m) { m.Entity<User>().HasIndex(x => x.Email).IsUnique(); m.Entity<Booking>().HasIndex(x => new { x.RoomId, x.StartsAt, x.EndsAt }); } }
public static class ClaimsExtensions { public static Guid TenantId(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue("tenant_id")!); public static Guid UserId(this ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!); }
public static class PasswordHasher { public static string Hash(string value) { var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16); return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(value, salt, 100000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32)); } public static bool Verify(string value, string hash) { var x = hash.Split('.'); return x.Length == 2 && Convert.ToBase64String(System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(value, Convert.FromBase64String(x[0]), 100000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32)) == x[1]; } }
public record RoomEdit(string Name, int Capacity, Guid? TariffId, bool IsActive);
public record TariffEdit(string Name, decimal HourlyRate);
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
