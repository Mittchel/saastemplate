using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Saas.Business.Services.Interfaces;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Data;
using Saas.Data.Extensions;
using Saas.Data.Interceptors;
using Saas.Data.Models;

namespace Saas.Business.Services;

/// <inheritdoc />
public partial class ProvisioningService(ApplicationDbContext context) : IProvisioningService
{
    /// <inheritdoc />
    public Task<OrganizationEntity> CreatePersonalOrganizationAsync(
        UserEntity user,
        CancellationToken cancellationToken = default)
    {
        var displayName = string.IsNullOrWhiteSpace(user.FullName)
            ? user.Email ?? "Workspace"
            : user.FullName;

        return CreateAsync(
            user.Id,
            $"{displayName}'s workspace",
            isPersonal: true,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<OrganizationEntity> CreateOrganizationAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new FunctionalException(
                "Organization name is required.",
                ErrorScenario.ValidationFailed);
        }

        return CreateAsync(userId, name.Trim(), isPersonal: false, cancellationToken);
    }

    /// <summary>Attempts before giving up on finding a free slug.</summary>
    private const int SlugAttempts = 5;

    private async Task<OrganizationEntity> CreateAsync(
        Guid userId,
        string name,
        bool isPersonal,
        CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(name);
        if (baseSlug.Length == 0)
        {
            baseSlug = "workspace";
        }

        // Slug collisions are resolved by catching the unique violation rather
        // than by checking first.
        //
        // A pre-flight "does this slug exist" query cannot work here: it runs as
        // the non-owner runtime role, whose policy on Organizations only admits
        // rows the caller is a member of, so another tenant's slug is invisible
        // and the check would report the name free right before the insert
        // fails. IgnoreQueryFilters() does not help -- it lifts EF's filter, not
        // the database's policy. Letting the unique index be the authority is
        // both correct under RLS and free of the check-then-act race.
        for (var attempt = 0; ; attempt++)
        {
            var organization = new OrganizationEntity
            {
                Name = name,
                IsPersonal = isPersonal,
                Slug = attempt == 0 ? baseSlug : $"{baseSlug}-{RandomSuffix()}",
            };

            try
            {
                // The membership below names an organization that is not the
                // caller's active tenant, so the RLS check would reject it.
                // Widening the tenant for just this transaction keeps the policy
                // strict for every other write. It has to be an explicit
                // transaction, because set_config(is_local) is discarded the
                // moment the transaction ends.
                await context.ExecuteInTransactionAsync(async token =>
                {
                    await context.SetLocalOrganizationAsync(organization.Id, token);
                    await context.SetLocalUserAsync(userId, token);

                    context.Organizations.Add(organization);
                    context.Memberships.Add(new MembershipEntity
                    {
                        OrganizationId = organization.Id,
                        UserId = userId,
                        Role = OrganizationRole.HrAdmin,
                    });

                    await context.SaveChangesAsync(token);
                }, cancellationToken);

                return organization;
            }
            catch (DbUpdateException exception)
                when (IsSlugConflict(exception) && attempt + 1 < SlugAttempts)
            {
                // The failed insert is still pending on the tracker; clear it so
                // the next attempt does not replay the rejected rows.
                context.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>True when the failure is the organization slug unique index.</summary>
    private static bool IsSlugConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } postgres
        && postgres.ConstraintName?.Contains("Slug", StringComparison.OrdinalIgnoreCase) == true;

    private static string RandomSuffix() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var slug = NonSlugCharacters()
            .Replace(builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), "-")
            .Trim('-');

        return slug.Length > 80 ? slug[..80].Trim('-') : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
