using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saas.Business.Contracts;
using Saas.Business.Mapping;
using Saas.Business.Services.Interfaces;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Options;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models;

namespace Saas.Business.Services;

/// <inheritdoc />
public class OrganizationService(
    ApplicationDbContext context,
    ITenantContext tenantContext,
    IProvisioningService provisioningService,
    IOptions<FeatureOptions> featureOptions) : IOrganizationService
{
    private readonly FeatureOptions _features = featureOptions.Value;

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrganizationSummary>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        var memberships = await context.Memberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Organization.IsPersonal)
            .ThenBy(m => m.Organization.Name)
            .ToListAsync(cancellationToken);

        return memberships.Select(m => m.Organization.ToSummary(m.Role)).ToList();
    }

    /// <inheritdoc />
    public async Task<OrganizationDetail> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var organization = await context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            ?? throw FunctionalException.NotFound("Organization");

        var memberCount = await context.Memberships
            .CountAsync(m => m.OrganizationId == organizationId, cancellationToken);

        return organization.ToDetail(memberCount);
    }

    /// <inheritdoc />
    public async Task<OrganizationSummary> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        RequireOrganizationsEnabled();

        var userId = RequireUserId();
        var organization = await provisioningService.CreateOrganizationAsync(userId, name, cancellationToken);

        return organization.ToSummary(OrganizationRole.HrAdmin);
    }

    /// <inheritdoc />
    public async Task<OrganizationDetail> UpdateAsync(
        Guid organizationId,
        string name,
        CancellationToken cancellationToken = default)
    {
        await RequireHrAdminAsync(organizationId, cancellationToken);

        var organization = await context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            ?? throw FunctionalException.NotFound("Organization");

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new FunctionalException("Organization name is required.", ErrorScenario.ValidationFailed);
        }

        organization.Name = name.Trim();
        await context.SaveChangesAsync(cancellationToken);

        return await GetAsync(organizationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await RequireHrAdminAsync(organizationId, cancellationToken);

        var organization = await context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            ?? throw FunctionalException.NotFound("Organization");

        // A user must always retain somewhere to land after signing in.
        if (organization.IsPersonal)
        {
            throw FunctionalException.Forbidden(
                "A personal workspace cannot be deleted.",
                ErrorScenario.CannotLeavePersonalOrganization);
        }

        context.Organizations.Remove(organization);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationRole?> GetRoleAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        // Reached while the caller's active tenant is still the previous
        // organization, so this relies on the membership policy's "or belongs
        // to the calling user" clause rather than the organization clause.
        var membership = await context.Memberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.OrganizationId == organizationId,
                cancellationToken);

        return membership?.Role;
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveDefaultOrganizationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        var memberships = await context.Memberships
            .AsNoTracking()
            .Include(m => m.Organization)
            .Where(m => m.UserId == userId)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return null;
        }

        // Prefer where they left off, but only while the membership still holds.
        if (user?.LastActiveOrganizationId is { } lastActive
            && memberships.Any(m => m.OrganizationId == lastActive))
        {
            return lastActive;
        }

        return memberships
            .OrderByDescending(m => m.Organization.IsPersonal)
            .ThenBy(m => m.Organization.Name)
            .First()
            .OrganizationId;
    }

    private Guid RequireUserId() =>
        tenantContext.UserId ?? throw new FunctionalException(
            "No authenticated user.",
            ErrorScenario.InvalidCredentials,
            HttpStatusCode.Unauthorized);

    private void RequireOrganizationsEnabled()
    {
        if (!_features.Organizations)
        {
            throw FunctionalException.Forbidden(
                "Organization management is disabled for this deployment.",
                ErrorScenario.OrganizationsDisabled);
        }
    }

    private async Task RequireHrAdminAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(RequireUserId(), organizationId, cancellationToken)
            ?? throw FunctionalException.Forbidden(
                "You are not a member of this organization.",
                ErrorScenario.NotAMember);

        if (role != OrganizationRole.HrAdmin)
        {
            throw FunctionalException.Forbidden(
                "This action requires the HR admin role.",
                ErrorScenario.InsufficientOrganizationRole);
        }
    }
}
