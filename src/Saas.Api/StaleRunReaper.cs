using Microsoft.EntityFrameworkCore;
using Saas.Data;

namespace Saas.Api;

/// <summary>
/// Periodically fails runs that never reported a result.
/// </summary>
/// <remarks>
/// <para>
/// Without this, a run whose AI service died mid-stream stays <c>Running</c>
/// forever and the conversation never resolves in the UI. The AI service makes
/// a best effort to mark its own failures, but the case this exists for is
/// precisely the one where that effort cannot succeed.
/// </para>
/// <para>
/// The sweep itself is a <c>SECURITY DEFINER</c> function in the database --
/// see the <c>StaleRunReaper</c> migration -- because row-level security
/// deliberately prevents any ordinary role from seeing every tenant's runs.
/// </para>
/// </remarks>
public class StaleRunReaper(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<StaleRunReaper> logger) : BackgroundService
{
    private TimeSpan Interval =>
        configuration.GetValue<TimeSpan?>("Runs:ReaperInterval") ?? TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a run may sit in <c>Running</c> before it is presumed dead.
    /// </summary>
    /// <remarks>
    /// Comfortably longer than any plausible agent turn: failing a run that is
    /// merely slow would be worse than leaving a dead one a few minutes longer.
    /// </remarks>
    private TimeSpan Timeout =>
        configuration.GetValue<TimeSpan?>("Runs:StaleAfter") ?? TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the API finish starting -- and the migrations run -- before the
        // first sweep touches the database.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await ReapAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed sweep must not take the host down; the next tick
                // tries again.
                logger.LogError(exception, "The stale run sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ReapAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var affected = await context.Database
            .SqlQuery<int>($"SELECT app_reap_stale_runs({Timeout}::interval) AS \"Value\"")
            .SingleAsync(cancellationToken);

        if (affected > 0)
        {
            logger.LogWarning(
                "Timed out {Count} run(s) that had been running for more than {Timeout}.",
                affected,
                Timeout);
        }
    }
}
