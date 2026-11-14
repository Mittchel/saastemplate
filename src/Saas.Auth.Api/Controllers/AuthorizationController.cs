using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Saas.Auth.Api.Services;
using Saas.Auth.Api.Tenancy;
using Saas.Business.Services.Interfaces;
using Saas.Core.Constants;
using Saas.Data;
using Saas.Data.Models;
using Saas.Data.Models.Constants;

using Saas.Core.Extensions;

namespace Saas.Auth.Api.Controllers;

/// <summary>
/// The OAuth token endpoint.
/// </summary>
/// <remarks>
/// Serves two grants. <c>password</c> signs a user in from the credentials the
/// Next.js login form collected. <c>refresh_token</c> both renews an expiring
/// token and -- when given an <c>organization_id</c> parameter -- switches the
/// active organization.
/// <para>
/// Switching through the refresh grant rather than a bespoke custom grant is
/// deliberate: the client already holds a refresh token and already knows how
/// to call this endpoint, the membership check is the only extra step, and
/// token rotation comes along for free. A custom flow would add a concept
/// without adding a guarantee.
/// </para>
/// </remarks>
[ApiController]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
[Produces("application/json")]
public class AuthorizationController(
    ILogger<AuthorizationController> logger,
    UserManager<UserEntity> userManager,
    SignInManager<UserEntity> signInManager,
    IOrganizationService organizationService,
    IPlatformAdminService platformAdminService,
    IExternalIdentityValidator externalIdentityValidator,
    IExternalLoginService externalLoginService,
    MutableTenantContext tenantContext,
    ApplicationDbContext context) : Controller
{
    /// <summary>Parameter that turns a refresh into an organization switch.</summary>
    private const string OrganizationParameter = "organization_id";

    /// <summary>
    /// Grant type for social sign-in: exchange a provider's verified ID token
    /// for this application's tokens.
    /// </summary>
    public const string ExternalGrantType = "urn:saas:external";

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is not available.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrantAsync(request, cancellationToken);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshGrantAsync(request, cancellationToken);
        }

        if (string.Equals(request.GrantType, ExternalGrantType, StringComparison.Ordinal))
        {
            return await HandleExternalGrantAsync(request, cancellationToken);
        }

        return Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: Failure(
                OpenIddictConstants.Errors.UnsupportedGrantType,
                "Only the password, refresh_token and external grants are supported."));
    }

    private async Task<IActionResult> HandlePasswordGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var user = request.Username is null ? null : await userManager.FindByEmailAsync(request.Username);

        if (user is null)
        {
            // Same response as a wrong password, so the endpoint cannot be used
            // to enumerate which addresses are registered.
            return InvalidCredentials();
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user, request.Password ?? string.Empty, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            logger.LogInformation(
                "Failed password grant for {UserId} (lockedOut: {LockedOut})",
                user.Id, result.IsLockedOut);

            return InvalidCredentials();
        }

        // Business services below read memberships, whose RLS policy keys on
        // app.user_id -- publish the identity before querying.
        tenantContext.SetUser(user.Id);

        var organizationId = await organizationService.ResolveDefaultOrganizationAsync(
            user.Id, cancellationToken);

        if (organizationId is null)
        {
            // Every user gets a personal organization at signup, so this only
            // happens to a record created outside the normal flow.
            logger.LogError("User {UserId} has no organization membership.", user.Id);

            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: Failure(
                    OpenIddictConstants.Errors.InvalidGrant,
                    "This account has no workspace. Contact support."));
        }

        var principal = await BuildPrincipalAsync(user, organizationId.Value, request, cancellationToken);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var subject = result.Principal?.GetClaim(OpenIddictConstants.Claims.Subject);

        if (!Guid.TryParse(subject, out var userId))
        {
            return InvalidCredentials();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !await signInManager.CanSignInAsync(user))
        {
            return InvalidCredentials();
        }

        tenantContext.SetUser(user.Id);

        // Absent the parameter this is an ordinary renewal: keep the caller on
        // the organization the expiring token named.
        var requestedOrganization = (string?)request.GetParameter(OrganizationParameter);
        var currentOrganization = result.Principal?.GetClaim(SaasClaimTypes.OrganizationId);

        var targetOrganization = Guid.TryParse(requestedOrganization, out var requested)
            ? requested
            : Guid.TryParse(currentOrganization, out var current)
                ? current
                : await organizationService.ResolveDefaultOrganizationAsync(user.Id, cancellationToken);

        if (targetOrganization is null)
        {
            return InvalidCredentials();
        }

        // The authorization that makes switching safe. Without it, any holder of
        // a refresh token could mint a token for an arbitrary organization.
        var role = await organizationService.GetRoleAsync(
            user.Id, targetOrganization.Value, cancellationToken);

        // A platform operator may enter any workspace to support it. They enter
        // as a plain Member: the point is to see what the customer sees, and
        // granting Owner would let support quietly delete a tenant. The
        // existence check needs the elevated read, because an operator is not a
        // member and the ordinary lookup cannot tell "no such tenant" from
        // "not yours".
        if (role is null
            && await userManager.IsInRoleAsync(user, PlatformRoles.Administrator)
            && await platformAdminService.OrganizationExistsAsync(
                targetOrganization.Value, cancellationToken))
        {
            logger.LogWarning(
                "Platform admin {UserId} entered organization {OrganizationId}.",
                user.Id, targetOrganization);

            role = OrganizationRole.Member;
        }

        if (role is null)
        {
            logger.LogWarning(
                "User {UserId} attempted to switch into organization {OrganizationId} without membership.",
                user.Id, targetOrganization);

            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: Failure(
                    OpenIddictConstants.Errors.InvalidGrant,
                    "You are not a member of the requested organization."));
        }

        var principal = await BuildPrincipalAsync(
            user, targetOrganization.Value, request, cancellationToken, role);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Exchanges a social provider's ID token for this application's tokens.
    /// </summary>
    /// <remarks>
    /// The provider's token is validated here, against the provider's own
    /// signing keys, rather than being taken on trust from the frontend. That
    /// is the entire security of this path: anything can claim an email
    /// address, but only the provider can sign a token asserting one.
    /// </remarks>
    private async Task<IActionResult> HandleExternalGrantAsync(
        OpenIddictRequest request,
        CancellationToken cancellationToken)
    {
        var provider = (string?)request.GetParameter("provider");
        var idToken = (string?)request.GetParameter("id_token");

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(idToken))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: Failure(
                    OpenIddictConstants.Errors.InvalidRequest,
                    "Both provider and id_token are required."));
        }

        UserEntity user;

        try
        {
            var identity = await externalIdentityValidator.ValidateAsync(
                provider, idToken, cancellationToken);

            user = await externalLoginService.FindOrCreateAsync(identity, cancellationToken);
        }
        catch (ExternalLoginException exception)
        {
            logger.LogInformation(
                "Rejected a {Provider} sign-in: {Reason}", provider, exception.Message);

            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: Failure(OpenIddictConstants.Errors.InvalidGrant, exception.Message));
        }

        if (!await signInManager.CanSignInAsync(user))
        {
            return InvalidCredentials();
        }

        tenantContext.SetUser(user.Id);

        var organizationId = await organizationService.ResolveDefaultOrganizationAsync(
            user.Id, cancellationToken);

        if (organizationId is null)
        {
            logger.LogError("User {UserId} has no organization membership.", user.Id);

            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: Failure(
                    OpenIddictConstants.Errors.InvalidGrant,
                    "This account has no workspace. Contact support."));
        }

        var principal = await BuildPrincipalAsync(user, organizationId.Value, request, cancellationToken);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Builds the claims principal, including the tenant and the permissions
    /// implied by the caller's organization role.
    /// </summary>
    /// <remarks>
    /// Expanding the role into permission claims here means the resource API
    /// only ever checks permissions and never has to know about roles, and that
    /// a role change takes effect on the next token rather than requiring a
    /// lookup on every request.
    /// </remarks>
    private async Task<ClaimsPrincipal> BuildPrincipalAsync(
        UserEntity user,
        Guid organizationId,
        OpenIddictRequest request,
        CancellationToken cancellationToken,
        OrganizationRole? knownRole = null)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);

        var role = knownRole
            ?? await organizationService.GetRoleAsync(user.Id, organizationId, cancellationToken)
            ?? OrganizationRole.Member;

        // Identity maps the name claim to UserName, which here is the email
        // address. Auth.js surfaces this as session.user.name, so set it to the
        // display name instead of showing the user their own address twice.
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            principal.SetClaim(OpenIddictConstants.Claims.Name, user.FullName);
        }

        // Marks the operator. It does not widen what they can read -- they are
        // still scoped to one organization -- it gates the operator endpoints
        // and lets the switch below accept a workspace they do not belong to.
        if (await userManager.IsInRoleAsync(user, PlatformRoles.Administrator))
        {
            principal.SetClaim(SaasClaimTypes.PlatformAdmin, "true");
        }

        principal.SetClaim(SaasClaimTypes.OrganizationId, organizationId.ToString());
        principal.SetClaim(SaasClaimTypes.OrganizationRole, role.ToString());
        principal.SetClaims(
            SaasClaimTypes.Permission,
            OrganizationRolePermissions.For(role).ToImmutableArray());

        // offline_access is what makes a refresh token be issued, and the
        // refresh token is also the mechanism for switching organization --
        // so the frontend must always ask for it.
        var scopes = request.GetScopes();
        principal.SetScopes(scopes);
        principal.SetResources("saas.api");

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(DestinationsFor(claim, principal));
        }

        // Remember where the user was, so the next sign-in lands in the same
        // place. Written outside the tenant-scoped tables, so no RLS concern.
        if (user.LastActiveOrganizationId != organizationId)
        {
            user.LastActiveOrganizationId = organizationId;
            context.Users.Update(user);
            await context.SaveChangesAsync(cancellationToken);
        }

        return principal;
    }

    /// <summary>
    /// Decides which tokens each claim is written into.
    /// </summary>
    /// <remarks>
    /// Claims default to nowhere in OpenIddict, so anything the resource API or
    /// the AI service needs has to be routed to the access token explicitly.
    /// </remarks>
    private static IEnumerable<string> DestinationsFor(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case OpenIddictConstants.Claims.Name:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (principal.HasScope(OpenIddictConstants.Scopes.Profile))
                {
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                }

                yield break;

            case OpenIddictConstants.Claims.Email:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (principal.HasScope(OpenIddictConstants.Scopes.Email))
                {
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                }

                yield break;

            case OpenIddictConstants.Claims.Role:
                yield return OpenIddictConstants.Destinations.AccessToken;

                if (principal.HasScope(OpenIddictConstants.Scopes.Roles))
                {
                    yield return OpenIddictConstants.Destinations.IdentityToken;
                }

                yield break;

            case SaasClaimTypes.OrganizationId:
            case SaasClaimTypes.OrganizationRole:
            case SaasClaimTypes.Permission:
                // Needed by both the .NET API and the Python service.
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;

            // Never leak the security stamp; it is an internal invalidation token.
            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return OpenIddictConstants.Destinations.AccessToken;
                yield break;
        }
    }

    private IActionResult InvalidCredentials() => Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: Failure(
            OpenIddictConstants.Errors.InvalidGrant,
            "The email address or password is incorrect."));

    private static AuthenticationProperties Failure(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
}
