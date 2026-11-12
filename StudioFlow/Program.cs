using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStudioFlow();

var app = builder.Build();
await app.Services.InitializeDatabaseAsync();

app.UseSerilogRequestLogging();
app.Use(
    async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            return Task.CompletedTask;
        });
        await next(context);
    }
);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints();

await app.RunAsync();

public partial class Program { }
