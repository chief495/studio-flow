using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>Allows EF tooling to inspect the model without starting the web app or seeding data.</summary>
public sealed class StudioFlowDesignTimeFactory : IDesignTimeDbContextFactory<StudioFlowDbContext>
{
    public StudioFlowDbContext CreateDbContext(string[] args)
    {
        var connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=studioflow;Username=studioflow;Password=studioflow";
        var options = new DbContextOptionsBuilder<StudioFlowDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new StudioFlowDbContext(options);
    }
}
