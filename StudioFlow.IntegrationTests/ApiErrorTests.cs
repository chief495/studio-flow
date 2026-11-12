using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

public sealed class ApiErrorTests(BookingApiFixture fixture) : IClassFixture<BookingApiFixture>
{
    [Fact]
    public async Task Invalid_booking_returns_validation_problem()
    {
        using var client = await fixture.Login("alice");
        using var response = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(fixture.RoomA, fixture.Start, fixture.Start.AddMinutes(10))
        );
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task Malformed_JSON_returns_validation_problem_without_parser_details()
    {
        using var client = await fixture.Login("alice");
        using var response = await client.PostAsync(
            "/api/bookings",
            new StringContent("{invalid-json", Encoding.UTF8, "application/json")
        );
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("request", out _));
        Assert.DoesNotContain("JsonException", problem.ToString());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("CANCEL")]
    public async Task Missing_booking_returns_problem_details(string operation)
    {
        using var client = await fixture.Login("alice");
        var path = "/api/bookings/" + Guid.NewGuid();
        using var response = operation switch
        {
            "GET" => await client.GetAsync(path),
            "PUT" => await client.PutAsJsonAsync(
                path,
                new BookingRequest(fixture.RoomA, fixture.Start, fixture.Start.AddHours(1))
            ),
            _ => await client.PostAsync(path + "/cancel", null),
        };
        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Overlapping_booking_returns_conflict_problem()
    {
        using var client = await fixture.Login("alice");
        using var response = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(
                fixture.RoomA,
                fixture.Start.AddMinutes(15),
                fixture.Start.AddHours(1)
            )
        );
        await AssertProblem(response, HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("/api/auth/register", "{\"email\":\"invalid\",\"password\":\"short\"}")]
    [InlineData("/api/rooms", "{\"name\":\"\",\"capacity\":0}")]
    [InlineData("/api/tariffs", "{\"name\":\"\",\"hourlyRate\":-1}")]
    public async Task Resource_validation_uses_same_problem_format(string path, string body)
    {
        using var client = await fixture.Login("admin");
        using var response = await client.PostAsync(
            path,
            new StringContent(body, Encoding.UTF8, "application/json")
        );
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
    }

    [Theory]
    [InlineData("booking_no_overlap", "23P01", 409)]
    [InlineData("another_exclusion_constraint", "23P01", 500)]
    [InlineData("FK_Bookings_Rooms_RoomId", "23503", 500)]
    [InlineData(null, null, 500)]
    public async Task Database_errors_are_classified_by_exact_constraint_and_unknown_errors_are_logged(
        string? constraint,
        string? sqlState,
        int status
    )
    {
        const string secret = "PRIVATE_DATABASE_DETAIL";
        Exception exception = sqlState is null
            ? new InvalidOperationException(secret)
            : new DbUpdateException(
                secret,
                new PostgresException(
                    secret,
                    "ERROR",
                    "ERROR",
                    sqlState,
                    constraintName: constraint
                )
            );
        var logs = new ErrorLogger();
        await using var app = fixture.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var originalOptions = services.Single(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<StudioFlowDbContext>)
                );
                services.Replace(
                    ServiceDescriptor.Scoped<DbContextOptions<StudioFlowDbContext>>(provider =>
                    {
                        var options =
                            (DbContextOptions<StudioFlowDbContext>)
                                originalOptions.ImplementationFactory!(provider);
                        return new DbContextOptionsBuilder<StudioFlowDbContext>(options)
                            .AddInterceptors(new FailureOnSave(exception))
                            .Options;
                    })
                );
                services.Replace(ServiceDescriptor.Singleton<ILogger<ApiExceptionHandler>>(logs));
            })
        );
        using var authenticated = await fixture.Login("alice");
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = authenticated
            .DefaultRequestHeaders
            .Authorization;
        using var response = await client.PostAsJsonAsync(
            "/api/bookings",
            new BookingRequest(
                fixture.RoomA,
                fixture.Start.AddDays(40),
                fixture.Start.AddDays(40).AddHours(1)
            )
        );
        var problem = await AssertProblem(response, (HttpStatusCode)status);
        Assert.DoesNotContain(secret, problem.ToString());
        Assert.DoesNotContain(nameof(InvalidOperationException), problem.ToString());
        if (status == 500)
        {
            var entry = Assert.Single(logs.Errors);
            Assert.Same(exception, entry.Exception);
            Assert.Contains(problem.GetProperty("traceId").GetString()!, entry.Message);
        }
    }

    private static async Task<JsonElement> AssertProblem(
        HttpResponseMessage response,
        HttpStatusCode status
    )
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
        var requestId = Assert.Single(response.Headers.GetValues("X-Request-Id"));
        Assert.Equal(requestId, problem.GetProperty("traceId").GetString());
        return problem;
    }

    private sealed class FailureOnSave(Exception exception) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            // Let startup migration/seed checks finish; fail actual writes only.
            if (eventData.Context!.ChangeTracker.HasChanges())
                throw exception;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ErrorLogger : ILogger<ApiExceptionHandler>
    {
        public List<(string Message, Exception? Exception)> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Error)
                Errors.Add((formatter(state, exception), exception));
        }
    }
}
