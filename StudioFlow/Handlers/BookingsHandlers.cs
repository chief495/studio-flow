using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;


public static class BookingsHandlers
{
    public static void Map(WebApplication app)
    {
        app.MapPut("/api/bookings/{id:guid}", UpdateBooking).RequireAuthorization();
        app.MapGet("/api/bookings", async (ClaimsPrincipal p, StudioFlowDbContext db) => Results.Ok(await db.Bookings.Where(x => x.TenantId == p.TenantId()).Include(x => x.Room).OrderBy(x => x.StartsAt).ToListAsync())).RequireAuthorization();

        app.MapPost("/api/bookings", async (BookingRequest r, ClaimsPrincipal p, StudioFlowDbContext db, IValidator<BookingRequest> v) => { var vr = await v.ValidateAsync(r); if (!vr.IsValid) return Results.ValidationProblem(vr.ToDictionary()); var tenant = p.TenantId(); if (!await db.Rooms.AnyAsync(x => x.Id == r.RoomId && x.TenantId == tenant && x.IsActive)) return Results.NotFound("Room was not found."); if (await db.Bookings.AnyAsync(x => x.RoomId == r.RoomId && x.TenantId == tenant && x.Status == BookingStatus.Active && r.StartsAt < x.EndsAt && r.EndsAt > x.StartsAt)) return Results.Conflict("The room is already booked for this time range."); var b = new Booking { RoomId = r.RoomId, UserId = p.UserId(), TenantId = tenant, StartsAt = r.StartsAt.ToUniversalTime(), EndsAt = r.EndsAt.ToUniversalTime() }; db.Bookings.Add(b); await db.SaveChangesAsync(); return Results.Created($"/api/bookings/{b.Id}", b); }).RequireAuthorization();

        app.MapPost("/api/bookings/{id:guid}/cancel", async (Guid id, ClaimsPrincipal p, StudioFlowDbContext db) => { var b = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == p.TenantId()); if (b is null) return Results.NotFound(); if (b.UserId != p.UserId() && !p.IsInRole(Role.Admin.ToString()) && !p.IsInRole(Role.Manager.ToString())) return Results.Forbid(); b.Status = BookingStatus.Cancelled; await db.SaveChangesAsync(); return Results.Ok(b); }).RequireAuthorization();

    }

    private static async Task<IResult> UpdateBooking(
        Guid id,
        BookingRequest request,
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        IValidator<BookingRequest> validator)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(
            item => item.Id == id && item.TenantId == user.TenantId());
        if (booking is null) return Results.NotFound();
        if (booking.UserId != user.UserId() &&
            !user.IsInRole("Admin") && !user.IsInRole("Manager"))
        {
            return Results.Forbid();
        }
        if (booking.Status != BookingStatus.Active)
        {
            return Results.Conflict("Отменённое бронирование нельзя изменить.");
        }

        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
        if (!await db.Rooms.AnyAsync(room => room.Id == request.RoomId &&
                room.TenantId == user.TenantId() && room.IsActive))
        {
            return Results.NotFound("Комната недоступна.");
        }
        var overlaps = await db.Bookings.AnyAsync(item =>
            item.Id != id && item.RoomId == request.RoomId &&
            item.Status == BookingStatus.Active &&
            request.StartsAt < item.EndsAt && request.EndsAt > item.StartsAt);
        if (overlaps) return Results.Conflict("Это время уже занято.");

        booking.RoomId = request.RoomId;
        booking.StartsAt = request.StartsAt;
        booking.EndsAt = request.EndsAt;
        await db.SaveChangesAsync();
        return Results.Ok(booking);
    }
}
