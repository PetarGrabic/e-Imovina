using Microsoft.EntityFrameworkCore;

namespace eImovina.Api.Data;

/// <summary>
/// Dedicated migrate+seed runner invoked once from Program.cs (Development only). Not an
/// IHostedService/BackgroundService - this project has no separate always-running worker
/// process, so a plain static helper called before the request pipeline starts is the
/// simplest correct shape.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task MigrateAndSeedAsync(IServiceProvider rootServices, CancellationToken ct = default)
    {
        using var scope = rootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        logger.LogInformation("Applying pending EF Core migrations...");
        await db.Database.MigrateAsync(ct);

        logger.LogInformation("Seeding demo data (idempotent)...");
        await DemoDataSeeder.SeedAsync(db, ct);

        logger.LogInformation("Database ready.");
    }
}
