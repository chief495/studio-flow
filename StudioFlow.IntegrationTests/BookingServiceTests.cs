using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class BookingServiceTests(BookingApiFixture fixture)
    : IClassFixture<BookingApiFixture>
{
    [Fact]
    public async Task Create_move_and_cancel_use_availability_and_quote_current_tariff()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var tariff = new Tariff
        {
            Name = "Price test",
            HourlyRate = 1200,
            TenantId = fixture.StudioA,
        };
        var room = new Room
        {
            Name = "Price test",
            Capacity = 2,
            TenantId = fixture.StudioA,
            Tariff = tariff,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        var start = fixture.Start;
        using var client = await fixture.Login("alice");
        using var created = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(room.Id, start, start.AddMinutes(30))
        );
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var booking = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(600m, booking.GetProperty("estimatedCost").GetDecimal());
        Assert.Equal("RUB", booking.GetProperty("currency").GetString());
        var id = booking.GetProperty("id").GetGuid();

        using var occupied = await client.GetAsync(
            $"/api/rooms/{room.Id}/availability?date={start:yyyy-MM-dd}"
        );
        var intervals = await occupied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, intervals.GetArrayLength());
        using var moved = await client.PutAsJsonAsync(
            $"/api/bookings/{id}",
            new BookingRequest(room.Id, start.AddHours(1), start.AddMinutes(150))
        );
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var quote = await moved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1800m, quote.GetProperty("estimatedCost").GetDecimal());

        using var cancelled = await client.PostAsync($"/api/bookings/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var free = await client.GetFromJsonAsync<JsonElement>(
            $"/api/rooms/{room.Id}/availability?date={start:yyyy-MM-dd}"
        );
        Assert.Equal(0, free.GetArrayLength());
        using var rejected = await client.PutAsJsonAsync(
            $"/api/bookings/{id}",
            new BookingRequest(room.Id, start, start.AddHours(1))
        );
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }
}
