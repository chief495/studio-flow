using FluentValidation;
using Microsoft.EntityFrameworkCore;

public sealed class BookingService(StudioFlowDbContext db, IValidator<BookingRequest> validator)
{
    public async Task<List<BookingView>> ListAsync(
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        var bookings = await AccessibleBookings(actor)
            .AsNoTracking()
            .Include(booking => booking.Room)
            .ThenInclude(room => room!.Tariff)
            .OrderBy(booking => booking.StartsAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(ToView).ToList();
    }

    public async Task<BookingView> GetAsync(
        Guid id,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        var booking =
            await AccessibleBookings(actor)
                .AsNoTracking()
                .Include(item => item.Room)
                .ThenInclude(room => room!.Tariff)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("Бронирование не найдено.");

        return ToView(booking);
    }

    public async Task<BookingView> CreateAsync(
        BookingRequest request,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var room = await GetRoomAsync(request.RoomId, actor.TenantId, cancellationToken);
        await EnsureNoOverlapAsync(request, null, cancellationToken);

        var booking = new Booking
        {
            RoomId = room.Id,
            Room = room,
            UserId = actor.UserId,
            TenantId = actor.TenantId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Status = BookingStatus.Active,
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        return ToView(booking);
    }

    public async Task<BookingView> UpdateAsync(
        Guid id,
        BookingRequest request,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        var booking = await GetAccessibleBookingAsync(id, actor, cancellationToken);
        if (booking.Status != BookingStatus.Active)
        {
            throw new BookingConflictException("Отменённое бронирование нельзя изменить.");
        }

        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var room = await GetRoomAsync(request.RoomId, actor.TenantId, cancellationToken);
        await EnsureNoOverlapAsync(request, id, cancellationToken);

        booking.RoomId = room.Id;
        booking.Room = room;
        booking.StartsAt = request.StartsAt;
        booking.EndsAt = request.EndsAt;
        await db.SaveChangesAsync(cancellationToken);
        return ToView(booking);
    }

    public async Task<BookingView> CancelAsync(
        Guid id,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        var booking = await GetAccessibleBookingAsync(id, actor, cancellationToken);
        booking.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        return ToView(booking);
    }

    public async Task<List<OccupiedInterval>> GetAvailabilityAsync(
        Guid roomId,
        DateTime date,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        await GetRoomAsync(roomId, actor.TenantId, cancellationToken);
        var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        if (dayStart == DateTime.SpecifyKind(DateTime.MaxValue.Date, DateTimeKind.Utc))
        {
            throw new ValidationException("Выберите допустимую дату.");
        }

        var nextDay = dayStart.AddDays(1);
        return await db
            .Bookings.AsNoTracking()
            .Where(booking =>
                booking.RoomId == roomId
                && booking.TenantId == actor.TenantId
                && booking.Status == BookingStatus.Active
                && booking.StartsAt < nextDay
                && booking.EndsAt > dayStart
            )
            .OrderBy(booking => booking.StartsAt)
            .Select(booking => new OccupiedInterval(booking.StartsAt, booking.EndsAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BookingSlot>> GetHourlySlotsAsync(
        Guid roomId,
        DateTime date,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        var occupied = await GetAvailabilityAsync(roomId, date, actor, cancellationToken);
        var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        return Enumerable
            .Range(9, 13)
            .Select(hour =>
            {
                var start = dayStart.AddHours(hour);
                var end = start.AddHours(1);
                var isAvailable =
                    start > now
                    && !occupied.Any(interval =>
                        start < interval.EndsAt && end > interval.StartsAt
                    );
                return new BookingSlot(start, end, isAvailable);
            })
            .ToList();
    }

    private IQueryable<Booking> AccessibleBookings(BookingActor actor)
    {
        return db.Bookings.Where(booking =>
            booking.TenantId == actor.TenantId
            && (booking.UserId == actor.UserId || actor.CanManageStudio)
        );
    }

    private async Task<Booking> GetAccessibleBookingAsync(
        Guid id,
        BookingActor actor,
        CancellationToken cancellationToken
    )
    {
        var booking =
            await AccessibleBookings(actor)
                .Include(booking => booking.Room)
                .ThenInclude(room => room!.Tariff)
                .SingleOrDefaultAsync(booking => booking.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException("Бронирование не найдено.");
        return booking;
    }

    private async Task<Room> GetRoomAsync(
        Guid roomId,
        Guid tenantId,
        CancellationToken cancellationToken
    )
    {
        var room =
            await db
                .Rooms.Include(room => room.Tariff)
                .SingleOrDefaultAsync(
                    room => room.Id == roomId && room.TenantId == tenantId && room.IsActive,
                    cancellationToken
                ) ?? throw new ResourceNotFoundException("Комната недоступна.");
        return room;
    }

    private async Task EnsureNoOverlapAsync(
        BookingRequest request,
        Guid? excludedId,
        CancellationToken cancellationToken
    )
    {
        var overlaps = await db.Bookings.AnyAsync(
            booking =>
                booking.RoomId == request.RoomId
                && (!excludedId.HasValue || booking.Id != excludedId.Value)
                && booking.Status == BookingStatus.Active
                && request.StartsAt < booking.EndsAt
                && request.EndsAt > booking.StartsAt,
            cancellationToken
        );

        if (overlaps)
        {
            throw new BookingConflictException("Это время уже занято.");
        }
    }

    private static BookingView ToView(Booking booking)
    {
        var hourlyRate = booking.Room?.Tariff?.HourlyRate;
        var durationHours = (decimal)(booking.EndsAt - booking.StartsAt).TotalMinutes / 60m;
        decimal? estimatedCost = hourlyRate.HasValue
            ? decimal.Round(hourlyRate.Value * durationHours, 2, MidpointRounding.AwayFromZero)
            : null;

        return new BookingView(
            booking.Id,
            booking.RoomId,
            booking.Room,
            booking.UserId,
            booking.TenantId,
            booking.StartsAt,
            booking.EndsAt,
            booking.Status,
            estimatedCost,
            "RUB"
        );
    }
}
