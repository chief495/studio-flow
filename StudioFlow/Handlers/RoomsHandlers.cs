using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

public static class RoomsHandlers
{
    public static void Map(WebApplication app)
    {
        var rooms = app.MapGroup("/api/rooms").RequireAuthorization();
        rooms.MapGet("", ListRooms);
        rooms.MapGet("/{id:guid}/availability", GetAvailability);
        rooms.MapGet("/{id:guid}/slots", GetSlots);
        rooms
            .MapPost("", CreateRoom)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
        rooms
            .MapPut("/{id:guid}", UpdateRoom)
            .RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));
    }

    private static async Task<IResult> ListRooms(
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        CancellationToken cancellationToken
    )
    {
        var tenantId = user.TenantId();
        var rooms = await db
            .Rooms.AsNoTracking()
            .Where(room => room.TenantId == tenantId && room.IsActive)
            .Include(room => room.Tariff)
            .ToListAsync(cancellationToken);
        return Results.Ok(rooms);
    }

    private static async Task<IResult> GetAvailability(
        Guid id,
        DateTime date,
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var occupied = await service.GetAvailabilityAsync(
            id,
            date,
            BookingsHandlers.Actor(user),
            cancellationToken
        );
        return Results.Ok(occupied);
    }

    private static async Task<IResult> GetSlots(
        Guid id,
        DateTime date,
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var slots = await service.GetHourlySlotsAsync(
            id,
            date,
            BookingsHandlers.Actor(user),
            cancellationToken
        );
        return Results.Ok(slots);
    }

    private static async Task<IResult> CreateRoom(
        RoomRequest request,
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        IValidator<RoomRequest> validator,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var room = new Room
        {
            Name = request.Name,
            Capacity = request.Capacity,
            TenantId = user.TenantId(),
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/rooms/{room.Id}", room);
    }

    private static async Task<IResult> UpdateRoom(
        Guid id,
        RoomEdit request,
        ClaimsPrincipal user,
        StudioFlowDbContext db,
        IValidator<RoomEdit> validator,
        CancellationToken cancellationToken
    )
    {
        var tenantId = user.TenantId();
        var room =
            await db.Rooms.SingleOrDefaultAsync(
                item => item.Id == id && item.TenantId == tenantId,
                cancellationToken
            ) ?? throw new ResourceNotFoundException("Комната не найдена.");

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        if (request.TariffId.HasValue)
        {
            var tariffExists = await db.Tariffs.AnyAsync(
                tariff => tariff.Id == request.TariffId && tariff.TenantId == tenantId,
                cancellationToken
            );
            if (!tariffExists)
                throw new ResourceNotFoundException("Тариф не найден.");
        }

        room.Name = request.Name;
        room.Capacity = request.Capacity;
        room.IsActive = request.IsActive;
        room.TariffId = request.TariffId;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
