using Microsoft.EntityFrameworkCore;
using Saas.Business.Contracts;
using Saas.Business.Services.Interfaces;
using Saas.Data;

namespace Saas.Business.Services;

/// <inheritdoc />
public class PlatformAdminService(ApplicationDbContext context) : IPlatformAdminService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PlatformOrganizationSummary>> ListOrganizationsAsync(
        CancellationToken cancellationToken = default)
    {
        // A raw query against the SECURITY DEFINER function, not a DbSet: the
        // entity would come back filtered to the operator's own tenant.
        var rows = await context.Database
            .SqlQuery<PlatformOrganizationRow>(
                $"SELECT id, name, slug, is_personal, created_date, member_count, run_count FROM app_list_organizations()")
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new PlatformOrganizationSummary(
                r.Id, r.Name, r.Slug, r.Is_Personal, r.Created_Date, r.Member_Count, r.Run_Count))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> OrganizationExistsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var result = await context.Database
            .SqlQuery<bool>($"SELECT app_organization_exists({organizationId}) AS \"Value\"")
            .SingleAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Shape the function returns. Named to match the snake_case columns, which
    /// is how EF binds a raw result without an entity type.
    /// </summary>
    private sealed record PlatformOrganizationRow(
        Guid Id,
        string Name,
        string Slug,
        bool Is_Personal,
        DateTimeOffset Created_Date,
        long Member_Count,
        long Run_Count);
}
