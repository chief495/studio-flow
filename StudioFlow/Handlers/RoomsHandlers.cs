using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class RoomsHandlers
{
 public static void Map(WebApplication app)
 {
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

app.MapGet("/api/rooms", async (ClaimsPrincipal p, StudioFlowDbContext db) => Results.Ok(await db.Rooms.Where(x => x.TenantId == p.TenantId() && x.IsActive).Include(x => x.Tariff).ToListAsync())).RequireAuthorization();

app.MapPost("/api/rooms", async (RoomRequest r, ClaimsPrincipal p, StudioFlowDbContext db, IValidator<RoomRequest> v) => { if (!p.IsInRole(Role.Admin.ToString()) && !p.IsInRole(Role.Manager.ToString())) return Results.Forbid(); var vr = await v.ValidateAsync(r); if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary()); var room = new Room { Name = r.Name, Capacity = r.Capacity, TenantId = p.TenantId() }; db.Rooms.Add(room); await db.SaveChangesAsync(); return Results.Created($"/api/rooms/{room.Id}", room); }).RequireAuthorization();

 }
}
