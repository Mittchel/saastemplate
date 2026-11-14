using System.Security.Claims;
using Saas.Core.Constants;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Tenancy;

namespace Saas.Api.Tenancy;

/// <summary>
/// Resolves the tenant from the access token on the current request.
/// </summary>
/// <remarks>
/// The organization travels in the token as <c>org_id</c>, so no database
/// lookup is needed to establish it -- and the Python AI service resolves the
/// tenant from the same claim in the same token. Switching organization is
/// therefore a token operation, handled by the identity service, not a session
/// flag the API can be talked into changing.
/// </remarks>
public class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    /// <inheritdoc />
    public Guid? OrganizationId =>
        ReadGuidClaim(SaasClaimTypes.OrganizationId);

    /// <inheritdoc />
    public Guid? UserId =>
        ReadGuidClaim(ClaimTypes.NameIdentifier) ?? ReadGuidClaim("sub");

    /// <inheritdoc />
    public bool IsAmbient => OrganizationId is null;

    /// <inheritdoc />
    public bool IsPlatformAdmin =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(SaasClaimTypes.PlatformAdmin) == "true";

    /// <inheritdoc />
    public Guid RequireOrganizationId() =>
        OrganizationId ?? throw new FunctionalException(
            "No organization is in scope for this request.",
            ErrorScenario.NotAMember,
            System.Net.HttpStatusCode.Forbidden);

    private Guid? ReadGuidClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User?.FindFirstValue(claimType);

        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
