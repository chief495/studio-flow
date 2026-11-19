using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class RoomManagementTests(BookingApiFixture fixture)
    : IClassFixture<BookingApiFixture>
{
    [Theory]
    [InlineData("admin")]
    [InlineData("manager")]
    public async Task Staff_can_disable_list_and_reactivate_rooms(string role)
    {
        using var staff = await fixture.Login(role);
        using var client = await fixture.Login("alice");
        using var foreign = await fixture.Login("foreign-admin");
        using var created = await staff.PostAsJsonAsync(
            "/api/rooms",
            new { name = "Lifecycle", capacity = 3 }
        );
        created.EnsureSuccessStatusCode();
        var room = (await created.Content.ReadFromJsonAsync<Room>())!;
        try
        {
            using var disabled = await staff.PutAsJsonAsync(
                $"/api/rooms/{room.Id}",
                new RoomEdit(room.Name, room.Capacity, null, false)
            );
            Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
            Assert.DoesNotContain(
                (await staff.GetFromJsonAsync<List<Room>>("/api/rooms"))!,
                r => r.Id == room.Id
            );
            Assert.DoesNotContain(
                (await client.GetFromJsonAsync<List<Room>>("/api/rooms"))!,
                r => r.Id == room.Id
            );
            var all = (
                await staff.GetFromJsonAsync<List<Room>>("/api/rooms?includeInactive=true")
            )!;
            Assert.False(Assert.Single(all, r => r.Id == room.Id).IsActive);
            Assert.All(all, r => Assert.Equal(fixture.StudioA, r.TenantId));
            Assert.DoesNotContain(
                (await foreign.GetFromJsonAsync<List<Room>>("/api/rooms?includeInactive=true"))!,
                r => r.Id == room.Id
            );
            using var foreignUpdate = await foreign.PutAsJsonAsync(
                $"/api/rooms/{room.Id}",
                new RoomEdit(room.Name, room.Capacity, null, true)
            );
            Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
            using var clientUpdate = await client.PutAsJsonAsync(
                $"/api/rooms/{room.Id}",
                new RoomEdit(room.Name, room.Capacity, null, true)
            );
            Assert.Equal(HttpStatusCode.Forbidden, clientUpdate.StatusCode);
            using var rejected = await client.PostAsJsonAsync(
                "/api/bookings",
                new BookingRequest(room.Id, fixture.Start, fixture.Start.AddHours(1))
            );
            Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
            using var enabled = await staff.PutAsJsonAsync(
                $"/api/rooms/{room.Id}",
                new RoomEdit(room.Name, room.Capacity, null, true)
            );
            Assert.Equal(HttpStatusCode.NoContent, enabled.StatusCode);
            Assert.True(
                Assert
                    .Single(
                        (await client.GetFromJsonAsync<List<Room>>("/api/rooms"))!,
                        r => r.Id == room.Id
                    )
                    .IsActive
            );
            using var booked = await client.PostAsJsonAsync(
                "/api/bookings",
                new BookingRequest(room.Id, fixture.Start, fixture.Start.AddHours(1))
            );
            Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        }
        finally
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
            await db.Rooms.Where(r => r.Id == room.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Inactive_list_requires_staff_role_and_authentication()
    {
        using var client = await fixture.Login("alice");
        using var denied = await client.GetAsync("/api/rooms?includeInactive=true");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var anonymous = fixture.CreateClient();
        using var unauthorized = await anonymous.GetAsync("/api/rooms?includeInactive=true");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }
}
