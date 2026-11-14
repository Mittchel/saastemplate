using Microsoft.EntityFrameworkCore;

namespace Saas.Data.Extensions;

/// <summary>
/// Runs a unit of work inside an explicit transaction under the configured
/// retrying execution strategy.
/// </summary>
/// <remarks>
/// <para>
/// <c>EnableRetryOnFailure</c> rejects user-initiated transactions outright: a
/// retry would otherwise replay only part of a multi-statement transaction and
/// silently lose the rest. EF requires the whole transaction to be the unit
/// that gets retried, which is what this wrapper expresses.
/// </para>
/// <para>
/// Every bootstrap path needs it, because they all pair a tenant override with
/// the inserts it authorises -- see <see cref="Interceptors.TenantSession"/>.
/// </para>
/// <para>
/// On a retry the change tracker still holds the pending entities and their ids
/// were assigned by the application, so the attempt replays the same rows
/// rather than duplicating them.
/// </para>
/// </remarks>
public static class DbContextTransactionExtensions
{
    public static Task ExecuteInTransactionAsync(
        this DbContext context,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        // Re-entrant: a caller that has already opened a transaction -- for
        // example registration, which must create the user and provision their
        // workspace atomically -- joins it rather than nesting. EF throws on a
        // second BeginTransaction, and more importantly the outermost caller
        // should decide what the atomic unit is.
        if (context.Database.CurrentTransaction is not null)
        {
            return operation(cancellationToken);
        }

        var strategy = context.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            cancellationToken,
            async (token) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token);

                await operation(token);

                await transaction.CommitAsync(token);
            });
    }
}
