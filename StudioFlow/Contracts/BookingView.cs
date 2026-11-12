/// <summary>EstimatedCost is a quote at the current tariff, not a captured payment.</summary>
public sealed record BookingView(
    Guid Id,
    Guid RoomId,
    Room? Room,
    Guid UserId,
    Guid TenantId,
    DateTime StartsAt,
    DateTime EndsAt,
    BookingStatus Status,
    decimal? EstimatedCost,
    string Currency
);

public sealed record OccupiedInterval(DateTime StartsAt, DateTime EndsAt);

public sealed record BookingSlot(DateTime StartsAt, DateTime EndsAt, bool Available);
