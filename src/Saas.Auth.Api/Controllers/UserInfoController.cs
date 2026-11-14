using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Saas.Core.Constants;
using Saas.Data.Models;

using Saas.Core.Extensions;

namespace Saas.Auth.Api.Controllers;

/// <summary>The OpenID Connect userinfo endpoint.</summary>
/// <remarks>
/// Present for standards compliance and for Auth.js, which calls it after a
/// token exchange. The application itself reads the richer <c>GET /me</c> on the
/// resource API instead.
/// </remarks>
[ApiController]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class UserInfoController(UserManager<UserEntity> userManager) : Controller
{
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfo()
    {
        var user = await userManager.GetUserAsync(User);

        if (user is null)
        {
            return Challenge(
                authenticationSchemes: OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictValidationAspNetCoreConstants.Properties.Error] =
                        OpenIddictConstants.Errors.InvalidToken,
                    [OpenIddictValidationAspNetCoreConstants.Properties.ErrorDescription] =
                        "The access token no longer resolves to a user.",
                }));
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Claims.Subject] = user.Id.ToString(),
            [OpenIddictConstants.Claims.Name] = user.FullName,
            [OpenIddictConstants.Claims.Email] = user.Email ?? string.Empty,
            [OpenIddictConstants.Claims.EmailVerified] = user.EmailConfirmed,
        };

        // Lets the frontend read the active tenant straight from the session it
        // builds out of this response.
        if (User.FindFirst(SaasClaimTypes.OrganizationId)?.Value is { } organizationId)
        {
            claims[SaasClaimTypes.OrganizationId] = organizationId;
        }

        if (User.FindFirst(SaasClaimTypes.OrganizationRole)?.Value is { } organizationRole)
        {
            claims[SaasClaimTypes.OrganizationRole] = organizationRole;
        }

        return Ok(claims);
    }
}
