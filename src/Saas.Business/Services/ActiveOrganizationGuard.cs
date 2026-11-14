using Saas.Core.Exceptions;
using Saas.Core.Tenancy;

namespace Saas.Business.Services;

/// <summary>
/// Shared check that a route's organization id is the caller's active organization.
/// </summary>
internal static class ActiveOrganizationGuard
{
    /// <summary>
    /// Throws a 404 when <paramref name="organizationId"/> is not the token's
    /// organization. Answering 404 rather than 403 avoids confirming that a foreign
    /// organization exists. Call it before touching any data.
    /// </summary>
    public static void Require(ITenantContext tenantContext, Guid organizationId)
    {
        if (tenantContext.OrganizationId != organizationId)
        {
            throw FunctionalException.NotFound("Organization");
        }
    }
}
