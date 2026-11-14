using System.Net;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Tenancy;

namespace Saas.Auth.Api.Tenancy;

/// <summary>
/// A tenant context the identity service sets explicitly.
/// </summary>
/// <remarks>
/// <para>
/// The resource API derives the tenant from the access token on the request.
/// The identity service cannot: during a password grant there is no token yet,
/// and during an organization switch the token still names the <em>previous</em>
/// organization. So the token endpoint establishes the identity itself, once it
/// has authenticated the user, before calling any business service.
/// </para>
/// <para>
/// This matters for more than convenience. The membership row-level security
/// policy admits rows belonging to the calling user, which is what lets the
/// switch be validated at all -- and that clause reads
/// <c>app.user_id</c>, published from here.
/// </para>
/// <para>
/// Scoped, and therefore confined to one request.
/// </para>
/// </remarks>
public class MutableTenantContext : ITenantContext
{
    /// <inheritdoc />
    public Guid? OrganizationId { get; private set; }

    /// <inheritdoc />
    public Guid? UserId { get; private set; }

    /// <inheritdoc />
    public bool IsAmbient => OrganizationId is null;

    /// <inheritdoc />
    /// <remarks>Always false: the identity service never acts as an operator.</remarks>
    public bool IsPlatformAdmin => false;

    /// <summary>
    /// Establishes the authenticated user for the rest of this request.
    /// </summary>
    public void SetUser(Guid userId) => UserId = userId;

    /// <summary>Establishes the active organization for the rest of this request.</summary>
    public void SetOrganization(Guid? organizationId) => OrganizationId = organizationId;

    /// <inheritdoc />
    public Guid RequireOrganizationId() =>
        OrganizationId ?? throw new FunctionalException(
            "No organization is in scope.",
            ErrorScenario.NotAMember,
            HttpStatusCode.Forbidden);
}
