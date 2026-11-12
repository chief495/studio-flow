using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class BookingAccessTests(BookingApiFixture fixture) : IClassFixture<BookingApiFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData("alice")]
    [InlineData("bob")]
    [InlineData("outsider")]
    [InlineData("admin")]
    [InlineData("manager")]
    [InlineData("foreign-admin")]
    [InlineData("foreign-manager")]
    public async Task List_is_scoped_by_tenant_and_owner(string name)
    {
        using var client = await fixture.Login(name);
        var bookings = await client.GetFromJsonAsync<List<Booking>>("/api/bookings", JsonOptions);
        var expected = name switch
        {
            "alice" => new[] { fixture.BookingA },
            "bob" => new[] { fixture.BookingB },
            "outsider" => new[] { fixture.BookingOtherStudio },
            "foreign-admin" or "foreign-manager" => new[] { fixture.BookingOtherStudio },
            _ => new[] { fixture.BookingA, fixture.BookingB },
        };
        Assert.Equal(expected.Order(), bookings!.Select(item => item.Id).Order());
    }

    [Theory]
    [InlineData("bob", "GET")]
    [InlineData("bob", "PUT")]
    [InlineData("bob", "CANCEL")]
    [InlineData("outsider", "GET")]
    [InlineData("outsider", "PUT")]
    [InlineData("outsider", "CANCEL")]
    [InlineData("foreign-admin", "GET")]
    [InlineData("foreign-admin", "PUT")]
    [InlineData("foreign-admin", "CANCEL")]
    [InlineData("foreign-manager", "GET")]
    [InlineData("foreign-manager", "PUT")]
    [InlineData("foreign-manager", "CANCEL")]
    public async Task Other_clients_and_studios_cannot_read_or_mutate_booking(
        string name,
        string operation
    )
    {
        using var client = await fixture.Login(name);
        using var response = await Operate(client, fixture.BookingA, operation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var owner = await fixture.Login("alice");
        var unchanged = await owner.GetFromJsonAsync<Booking>(
            $"/api/bookings/{fixture.BookingA}",
            JsonOptions
        );
        Assert.Equal(fixture.Start, unchanged!.StartsAt);
        Assert.Equal(BookingStatus.Active, unchanged.Status);
    }

    [Theory]
    [InlineData("alice", "GET")]
    [InlineData("alice", "PUT")]
    [InlineData("alice", "CANCEL")]
    [InlineData("admin", "GET")]
    [InlineData("admin", "PUT")]
    [InlineData("admin", "CANCEL")]
    [InlineData("manager", "GET")]
    [InlineData("manager", "PUT")]
    [InlineData("manager", "CANCEL")]
    public async Task Owner_and_studio_staff_can_access_booking(string name, string operation)
    {
        // A separate booking for each case keeps shared isolation fixtures immutable.
        using var owner = await fixture.Login("alice");
        var start = fixture.Start.AddDays(1);
        using var created = await owner.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(fixture.RoomA, start, start.AddHours(1))
        );
        created.EnsureSuccessStatusCode();
        var booking = (await created.Content.ReadFromJsonAsync<Booking>(JsonOptions))!;
        try
        {
            using var client = await fixture.Login(name);
            using var response = await Operate(client, booking.Id, operation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = (await response.Content.ReadFromJsonAsync<Booking>(JsonOptions))!;
            Assert.Equal(booking.Id, result.Id);
            Assert.Equal(
                operation == "CANCEL" ? BookingStatus.Cancelled : BookingStatus.Active,
                result.Status
            );
            if (operation == "PUT")
                Assert.Equal(fixture.Start.AddDays(2), result.StartsAt);
        }
        finally
        {
            await owner.PostAsync($"/api/bookings/{booking.Id}/cancel", null);
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
            await db.Bookings.Where(item => item.Id == booking.Id).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("CANCEL")]
    public async Task Anonymous_requests_are_rejected(string operation)
    {
        using var client = fixture.CreateClient();
        using var response = await Operate(client, fixture.BookingA, operation);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var list = await client.GetAsync("/api/bookings");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_create_booking_in_another_studio()
    {
        using var client = await fixture.Login("alice");
        using var response = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(
                fixture.RoomB,
                fixture.Start.AddDays(3),
                fixture.Start.AddDays(3).AddHours(1)
            )
        );
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_cannot_move_booking_to_another_studio()
    {
        using var client = await fixture.Login("alice");
        using var response = await client.PutAsJsonAsync(
            $"/api/bookings/{fixture.BookingA}",
            new BookingRequest(
                fixture.RoomB,
                fixture.Start.AddDays(3),
                fixture.Start.AddDays(3).AddHours(1)
            )
        );
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await client.GetFromJsonAsync<Booking>(
            $"/api/bookings/{fixture.BookingA}",
            JsonOptions
        );
        Assert.Equal(fixture.RoomA, unchanged!.RoomId);
        Assert.Equal(fixture.StudioA, unchanged.TenantId);
        Assert.Equal(fixture.Start, unchanged.StartsAt);
    }

    [Fact]
    public async Task Registration_cannot_assign_tenant_or_privileged_role()
    {
        using var client = fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email = "intruder@tests.local",
                password = "Password123!",
                tenantId = fixture.StudioA,
                role = "Admin",
            }
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var user = await db.Users.SingleAsync(user => user.Email == "intruder@tests.local");
        Assert.Equal(DemoStudio.Id, user.TenantId);
        Assert.Equal(Role.Client, user.Role);
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "intruder@tests.local", password = "Password123!" }
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                json.GetProperty("token").GetString()
            );
        using var otherBooking = await client.GetAsync($"/api/bookings/{fixture.BookingA}");
        Assert.Equal(HttpStatusCode.NotFound, otherBooking.StatusCode);
        var bookings = await client.GetFromJsonAsync<List<Booking>>("/api/bookings", JsonOptions);
        Assert.Empty(bookings!);
        using var manage = await client.PostAsJsonAsync(
            "/api/tariffs",
            new { name = "Not allowed", hourlyRate = 100 }
        );
        Assert.Equal(HttpStatusCode.Forbidden, manage.StatusCode);
    }

    private Task<HttpResponseMessage> Operate(HttpClient client, Guid id, string operation)
    {
        var path = $"/api/bookings/{id}";
        return operation switch
        {
            "GET" => client.GetAsync(path),
            "PUT" => client.PutAsJsonAsync(
                path,
                new BookingRequest(
                    fixture.RoomA,
                    fixture.Start.AddDays(2),
                    fixture.Start.AddDays(2).AddHours(1)
                )
            ),
            "CANCEL" => client.PostAsync(path + "/cancel", null),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }
}
