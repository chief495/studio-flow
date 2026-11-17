/// <summary>Stored booking price in RUB; EstimatedCost is a compatibility alias for TotalCost.</summary>
public sealed record BookingView(
    Guid Id,
    Guid RoomId,
    Room? Room,
    Guid UserId,
    Guid TenantId,
    DateTime StartsAt,
    DateTime EndsAt,
    BookingStatus Status,
    decimal? AppliedHourlyRate,
    decimal? TotalCost,
    decimal? EstimatedCost,
    string Currency
);

public sealed record OccupiedInterval(DateTime StartsAt, DateTime EndsAt);

public sealed record BookingSlot(DateTime StartsAt, DateTime EndsAt, bool Available);
