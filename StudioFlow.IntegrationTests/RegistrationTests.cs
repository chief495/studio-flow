using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class RegistrationTests(BookingApiFixture fixture) : IClassFixture<BookingApiFixture>
{
    [Fact]
    public async Task New_client_can_register_login_and_use_returned_JWT()
    {
        using var client = fixture.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@tests.local";
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "  " + email.ToUpperInvariant() + "  ", password = "NewPassword123!" }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, data.GetProperty("email").GetString());
        Assert.Equal("Client", data.GetProperty("role").GetString());
        Assert.Equal(DemoStudio.Id, data.GetProperty("tenantId").GetGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            data.GetProperty("token").GetString()
        );
        using var rooms = await client.GetAsync("/api/rooms");
        Assert.Equal(HttpStatusCode.OK, rooms.StatusCode);
        var availableRooms = (await rooms.Content.ReadFromJsonAsync<List<Room>>())!;
        var room = Assert.Single(availableRooms);
        using var booking = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(room.Id, fixture.Start, fixture.Start.AddHours(1))
        );
        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
        var saved = await booking.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(data.GetProperty("id").GetGuid(), saved.GetProperty("userId").GetGuid());
        Assert.Equal(DemoStudio.Id, saved.GetProperty("tenantId").GetGuid());
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "NewPassword123!" }
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var user = await db.Users.SingleAsync(item => item.Email == email);
        Assert.NotEqual("NewPassword123!", user.PasswordHash);
        Assert.True(PasswordHasher.Verify("NewPassword123!", user.PasswordHash));
    }

    [Theory]
    [InlineData("invalid", "Password123!")]
    [InlineData("valid@tests.local", "short")]
    [InlineData("", "Password123!")]
    [InlineData("valid@tests.local", "")]
    public async Task Invalid_registration_returns_validation_error(string email, string password)
    {
        using var client = fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        Assert.False(await db.Users.AnyAsync(item => item.Email == email));
    }

    [Fact]
    public async Task Concurrent_duplicate_email_creates_only_one_account()
    {
        using var client = fixture.CreateClient();
        var email = Guid.NewGuid().ToString("N") + "@tests.local";
        var responses = await Task.WhenAll(
            Enumerable
                .Range(0, 4)
                .Select(_ =>
                    client.PostAsJsonAsync(
                        "/api/auth/register",
                        new { email, password = "Password123!" }
                    )
                )
        );
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(
                3,
                responses.Count(response => response.StatusCode == HttpStatusCode.Conflict)
            );
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
            Assert.Equal(1, await db.Users.CountAsync(user => user.Email == email));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }
}
