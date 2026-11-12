using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

public sealed class MigrationTests(BookingApiFixture fixture) : IClassFixture<BookingApiFixture>
{
    [Fact]
    public async Task Empty_database_startup_applies_all_migrations_and_is_repeatable()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(2, applied.Length);
        Assert.EndsWith("_InitialSchema", applied[0]);
        Assert.EndsWith("_PreventOverlappingBookings", applied[1]);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var before = await Snapshot.Read(db);
        await fixture.Services.InitializeDatabaseAsync();
        var after = await Snapshot.Read(db);
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
    }

    [Fact]
    public async Task Constraint_migration_can_be_reverted_and_reapplied_without_data_loss()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var before = JsonSerializer.Serialize(await Snapshot.Read(db));
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await db.GetService<IMigrator>().MigrateAsync(migrations[0]);
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(before, JsonSerializer.Serialize(await Snapshot.Read(db)));
        Assert.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task Migrated_database_rejects_overlaps_but_allows_adjacent_and_cancelled_bookings()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
        var existing = await db
            .Bookings.AsNoTracking()
            .SingleAsync(item => item.Id == fixture.BookingA);
        var conflict = CopyBooking(
            existing,
            existing.StartsAt.AddMinutes(15),
            BookingStatus.Active
        );
        db.Bookings.Add(conflict);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23P01", Assert.IsType<PostgresException>(error.InnerException).SqlState);
        db.ChangeTracker.Clear();

        var adjacent = CopyBooking(existing, existing.EndsAt, BookingStatus.Active);
        var cancelled = CopyBooking(existing, existing.StartsAt, BookingStatus.Cancelled);
        db.Bookings.AddRange(adjacent, cancelled);
        await db.SaveChangesAsync();
        await db
            .Bookings.Where(item => item.Id == adjacent.Id || item.Id == cancelled.Id)
            .ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Legacy_data_copy_into_migrated_database_preserves_every_row()
    {
        var adminConnection =
            Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=55432;Database=postgres;Username=studioflow_test;Password=studioflow_test";
        var legacyName = "studioflow_test_legacy_" + Guid.NewGuid().ToString("N");
        var targetName = "studioflow_test_copy_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await Execute(admin, $"CREATE DATABASE {legacyName}");
        await Execute(admin, $"CREATE DATABASE {targetName}");
        try
        {
            using var scope = fixture.Services.CreateScope();
            var populated = scope.ServiceProvider.GetRequiredService<StudioFlowDbContext>();
            await using var legacy = Context(adminConnection, legacyName);
            // Intentional test-only reproduction of the pre-migration application.
            await legacy.Database.EnsureCreatedAsync();
            await legacy.Database.ExecuteSqlRawAsync(
                """
                CREATE EXTENSION IF NOT EXISTS btree_gist;
                ALTER TABLE "Bookings" ADD CONSTRAINT booking_no_overlap EXCLUDE USING gist
                ("RoomId" WITH =, tstzrange("StartsAt", "EndsAt", '[)') WITH &&) WHERE ("Status" = 0);
                """
            );
            await (await Snapshot.Read(populated)).Insert(legacy);
            var original = await Snapshot.Read(legacy);

            await using var target = Context(adminConnection, targetName);
            await target.Database.MigrateAsync();
            // The transfer carries data only, never legacy DDL or migration history.
            await (await Snapshot.Read(legacy)).Insert(target);
            await target.Database.MigrateAsync();
            Assert.Equal(
                JsonSerializer.Serialize(original),
                JsonSerializer.Serialize(await Snapshot.Read(target))
            );
            Assert.Equal(
                JsonSerializer.Serialize(original),
                JsonSerializer.Serialize(await Snapshot.Read(legacy))
            );
            Assert.Equal(2, (await target.Database.GetAppliedMigrationsAsync()).Count());
            var booking = original.Bookings[0];
            target.Bookings.Add(CopyBooking(booking, booking.StartsAt, BookingStatus.Active));
            var error = await Assert.ThrowsAsync<DbUpdateException>(
                () => target.SaveChangesAsync()
            );
            Assert.Equal("23P01", Assert.IsType<PostgresException>(error.InnerException).SqlState);
        }
        finally
        {
            await Execute(admin, $"DROP DATABASE {targetName} WITH (FORCE)");
            await Execute(admin, $"DROP DATABASE {legacyName} WITH (FORCE)");
        }
    }

    private static StudioFlowDbContext Context(string connection, string database) =>
        new(
            new DbContextOptionsBuilder<StudioFlowDbContext>()
                .UseNpgsql(
                    new NpgsqlConnectionStringBuilder(connection)
                    {
                        Database = database,
                    }.ConnectionString
                )
                .Options
        );

    private static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static Booking CopyBooking(Booking source, DateTime start, BookingStatus status) =>
        new()
        {
            RoomId = source.RoomId,
            TenantId = source.TenantId,
            UserId = source.UserId,
            StartsAt = start,
            EndsAt = start.AddHours(1),
            Status = status,
        };

    private sealed record Snapshot(
        List<User> Users,
        List<Tariff> Tariffs,
        List<Room> Rooms,
        List<Booking> Bookings
    )
    {
        public static async Task<Snapshot> Read(StudioFlowDbContext db) =>
            new(
                await db.Users.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
                await db.Tariffs.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
                await db.Rooms.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
                await db.Bookings.AsNoTracking().OrderBy(item => item.Id).ToListAsync()
            );

        public async Task Insert(StudioFlowDbContext db)
        {
            db.Users.AddRange(Users);
            db.Tariffs.AddRange(Tariffs);
            db.Rooms.AddRange(Rooms);
            db.Bookings.AddRange(Bookings);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }
}
