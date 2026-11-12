using System.Security.Claims;

public static class BookingsHandlers
{
    public static void Map(WebApplication app)
    {
        var bookings = app.MapGroup("/api/bookings").RequireAuthorization();
        bookings.MapGet("", ListBookings);
        bookings.MapGet("/{id:guid}", GetBooking);
        bookings.MapPost("", CreateBooking);
        bookings.MapPut("/{id:guid}", UpdateBooking);
        bookings.MapPost("/{id:guid}/cancel", CancelBooking);
    }

    public static BookingActor Actor(ClaimsPrincipal user) =>
        new(
            user.UserId(),
            user.TenantId(),
            user.IsInRole(nameof(Role.Admin)) || user.IsInRole(nameof(Role.Manager))
        );

    private static async Task<IResult> ListBookings(
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var bookings = await service.ListAsync(Actor(user), cancellationToken);
        return Results.Ok(bookings);
    }

    private static async Task<IResult> GetBooking(
        Guid id,
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var booking = await service.GetAsync(id, Actor(user), cancellationToken);
        return Results.Ok(booking);
    }

    private static async Task<IResult> CreateBooking(
        BookingRequest request,
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var booking = await service.CreateAsync(request, Actor(user), cancellationToken);
        return Results.Created($"/api/bookings/{booking.Id}", booking);
    }

    private static async Task<IResult> UpdateBooking(
        Guid id,
        BookingRequest request,
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var booking = await service.UpdateAsync(id, request, Actor(user), cancellationToken);
        return Results.Ok(booking);
    }

    private static async Task<IResult> CancelBooking(
        Guid id,
        ClaimsPrincipal user,
        BookingService service,
        CancellationToken cancellationToken
    )
    {
        var booking = await service.CancelAsync(id, Actor(user), cancellationToken);
        return Results.Ok(booking);
    }
}
