using Microsoft.EntityFrameworkCore;

namespace Saas.Data.Interceptors;

/// <summary>
/// Overrides the tenant for the current transaction.
/// </summary>
/// <remarks>
/// <para>
/// Needed for the two bootstrap operations that legitimately act outside the
/// caller's active organization: signing up (create user, personal
/// organization and owner membership) and creating an additional organization.
/// Both insert a membership row whose <c>OrganizationId</c> is not yet the
/// active tenant, which the row-level security check correctly rejects.
/// </para>
/// <para>
/// The alternative -- a permissive INSERT policy on <c>Memberships</c> -- would
/// leave the one table where a forged row grants cross-tenant access
/// unprotected, defeating the point of the second isolation layer. Narrowing
/// the override to a single transaction keeps the policy strict.
/// </para>
/// <para>
/// Writes with <c>is_local = true</c>, so the override is reverted when the
/// transaction ends. It therefore only has an effect inside an explicit
/// transaction; callers must open one first.
/// </para>
/// </remarks>
public static class TenantSession
{
    /// <summary>
    /// Points <c>app.organization_id</c> at <paramref name="organizationId"/> for
    /// the remainder of the current transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public static async Task SetLocalOrganizationAsync(
        this DbContext context,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "SetLocalOrganizationAsync requires an open transaction; otherwise the " +
                "override is discarded immediately and the insert still fails RLS.");
        }

        await context.Database.ExecuteSqlAsync(
            $"SELECT set_config('app.organization_id', {organizationId.ToString()}, true)",
            cancellationToken);
    }

    /// <summary>
    /// Points <c>app.user_id</c> at <paramref name="userId"/> for the remainder of
    /// the current transaction. Used during signup, where no user is
    /// authenticated yet but the new member row must satisfy the policy.
    /// </summary>
    public static async Task SetLocalUserAsync(
        this DbContext context,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "SetLocalUserAsync requires an open transaction.");
        }

        await context.Database.ExecuteSqlAsync(
            $"SELECT set_config('app.user_id', {userId.ToString()}, true)",
            cancellationToken);
    }
}
