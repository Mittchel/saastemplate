using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Data.Models;
using Saas.Data.Models.Constants;

namespace Saas.Api;

/// <summary>
/// Startup migration and reference-data seeding.
/// </summary>
/// <remarks>
/// <para>
/// Migrations run on a context built from the <c>Migrations</c> connection
/// string, not the one the request pipeline uses. The runtime connects as
/// <c>saas_app</c>, which deliberately does not own the tables -- that is what
/// makes row-level security apply to it -- and therefore cannot create or alter
/// them. Keeping both strings lets a single <c>docker compose up</c> bring up a
/// working database without a separate migration step, at the cost of the API
/// knowing the owner credentials.
/// </para>
/// <para>
/// For a deployment where the API must not hold owner credentials, drop
/// <c>ConnectionStrings:Migrations</c> and run <c>dotnet ef database update</c>
/// (or a migration bundle) as a release step instead; this worker then only
/// seeds.
/// </para>
/// </remarks>
public class Worker(
    IConfiguration configuration,
    ILogger<Worker> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var migrationsConnectionString = configuration.GetConnectionString("Migrations");

        if (string.IsNullOrWhiteSpace(migrationsConnectionString))
        {
            logger.LogInformation(
                "No Migrations connection string configured; skipping automatic migration.");
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(migrationsConnectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} migration(s): {Migrations}",
            pending.Count, string.Join(", ", pending));

        await context.Database.MigrateAsync(cancellationToken);
    }

}
