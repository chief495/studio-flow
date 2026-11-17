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
    public async Task Booking_without_tariff_stays_unpriced_after_tariff_is_assigned()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var room = new Room
        {
            Name = "Unpriced",
            Capacity = 2,
            TenantId = fixture.StudioA,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        using var client = await fixture.Login("alice");
        using var created = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(room.Id, fixture.Start, fixture.Start.AddHours(1))
        );
        created.EnsureSuccessStatusCode();
        var booking = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, booking.GetProperty("totalCost").ValueKind);
        room.Tariff = new Tariff
        {
            Name = "Assigned later",
            HourlyRate = 1500,
            TenantId = fixture.StudioA,
        };
        db.Tariffs.Add(room.Tariff);
        await db.SaveChangesAsync();
        var id = booking.GetProperty("id").GetGuid();
        using var moved = await client.PutAsJsonAsync(
            $"/api/bookings/{id}",
            new BookingRequest(room.Id, fixture.Start.AddHours(2), fixture.Start.AddHours(4))
        );
        moved.EnsureSuccessStatusCode();
        var result = await moved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, result.GetProperty("appliedHourlyRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, result.GetProperty("totalCost").ValueKind);
    }

    [Fact]
    public async Task Create_move_and_cancel_use_availability_and_preserve_captured_price()
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
        tariff.HourlyRate = 2400;
        await db.SaveChangesAsync();
        var unchanged = await client.GetFromJsonAsync<JsonElement>($"/api/bookings/{id}");
        Assert.Equal(1200m, unchanged.GetProperty("appliedHourlyRate").GetDecimal());
        Assert.Equal(600m, unchanged.GetProperty("totalCost").GetDecimal());
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/bookings");
        Assert.Equal(
            600m,
            listed
                .EnumerateArray()
                .Single(b => b.GetProperty("id").GetGuid() == id)
                .GetProperty("totalCost")
                .GetDecimal()
        );
        using var moved = await client.PutAsJsonAsync(
            $"/api/bookings/{id}",
            new BookingRequest(room.Id, start.AddHours(1), start.AddMinutes(150))
        );
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var quote = await moved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1800m, quote.GetProperty("estimatedCost").GetDecimal());

        Assert.Equal(1200m, quote.GetProperty("appliedHourlyRate").GetDecimal());
        Assert.Equal(1800m, quote.GetProperty("totalCost").GetDecimal());

        var otherRoom = new Room
        {
            Name = "Other price test",
            Capacity = 2,
            TenantId = fixture.StudioA,
            Tariff = tariff,
        };
        db.Rooms.Add(otherRoom);
        await db.SaveChangesAsync();
        using var changedRoom = await client.PutAsJsonAsync(
            $"/api/bookings/{id}",
            new BookingRequest(otherRoom.Id, start.AddHours(1), start.AddMinutes(150))
        );
        changedRoom.EnsureSuccessStatusCode();
        var repriced = await changedRoom.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2400m, repriced.GetProperty("appliedHourlyRate").GetDecimal());
        Assert.Equal(3600m, repriced.GetProperty("totalCost").GetDecimal());

        using var cancelled = await client.PostAsync($"/api/bookings/{id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledBooking = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3600m, cancelledBooking.GetProperty("totalCost").GetDecimal());
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
