using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

public sealed class BookingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Password = "IntegrationPassword123!";
    private readonly string databaseName = "studioflow_test_" + Guid.NewGuid().ToString("N");
    private readonly string adminConnection =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
        ?? "Host=localhost;Port=55432;Database=postgres;Username=studioflow_test;Password=studioflow_test";

    public Guid StudioA { get; } = Guid.NewGuid();
    public Guid StudioB { get; } = Guid.NewGuid();
    public Guid RoomA { get; } = Guid.NewGuid();
    public Guid RoomB { get; } = Guid.NewGuid();
    public Guid BookingA { get; } = Guid.NewGuid();
    public Guid BookingB { get; } = Guid.NewGuid();
    public Guid BookingOtherStudio { get; } = Guid.NewGuid();
    public DateTime Start { get; } = DateTime.UtcNow.Date.AddDays(10).AddHours(10);

    private string TestConnection =>
        new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = databaseName,
        }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<StudioFlowDbContext>>();
            services.RemoveAll<StudioFlowDbContext>();
            services.AddDbContext<StudioFlowDbContext>(options =>
                options.UseNpgsql(TestConnection)
            );
        });
    }

    public async Task InitializeAsync()
    {
        // Each run owns a fresh database. Never reset the application's database.
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
        await command.ExecuteNonQueryAsync();

        using var client = CreateClient(); // Starts the real app, including schema initialization.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var users = new[]
        {
            MakeUser("alice", StudioA, Role.Client),
            MakeUser("bob", StudioA, Role.Client),
            MakeUser("outsider", StudioB, Role.Client),
            MakeUser("admin", StudioA, Role.Admin),
            MakeUser("manager", StudioA, Role.Manager),
            MakeUser("foreign-admin", StudioB, Role.Admin),
            MakeUser("foreign-manager", StudioB, Role.Manager),
        };
        db.Users.AddRange(users);
        db.Rooms.AddRange(
            new Room
            {
                Id = RoomA,
                Name = "Studio A room",
                Capacity = 4,
                TenantId = StudioA,
            },
            new Room
            {
                Id = RoomB,
                Name = "Studio B room",
                Capacity = 4,
                TenantId = StudioB,
            }
        );
        db.Bookings.AddRange(
            MakeBooking(BookingA, users[0], RoomA, Start),
            MakeBooking(BookingB, users[1], RoomA, Start.AddHours(2)),
            MakeBooking(BookingOtherStudio, users[2], RoomB, Start)
        );
        await db.SaveChangesAsync();
    }

    public async Task<HttpClient> Login(string name)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = name + "@tests.local", password = Password }
        );
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<LoginResult>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            session!.Token
        );
        return client;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        // The name is generated locally and restricted to this fixture's database.
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE {databaseName} WITH (FORCE)",
            connection
        );
        await command.ExecuteNonQueryAsync();
    }

    private static User MakeUser(string name, Guid tenantId, Role role) =>
        new()
        {
            Email = name + "@tests.local",
            PasswordHash = PasswordHasher.Hash(Password),
            TenantId = tenantId,
            Role = role,
        };

    private static Booking MakeBooking(Guid id, User user, Guid roomId, DateTime start) =>
        new()
        {
            Id = id,
            UserId = user.Id,
            TenantId = user.TenantId,
            RoomId = roomId,
            StartsAt = start,
            EndsAt = start.AddHours(1),
            Status = BookingStatus.Active,
        };

    private sealed record LoginResult(string Token);
}
