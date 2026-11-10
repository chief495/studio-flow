using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddStudioFlow();

var app = builder.Build();
await app.Services.InitializeDatabaseAsync();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseBookingConflicts();
app.MapApiEndpoints();

await app.RunAsync();
