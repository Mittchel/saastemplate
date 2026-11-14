using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Saas.Business.Contracts;
using Saas.Core.Options;

namespace Saas.Api.Controllers;

/// <summary>
/// The deployment's feature flags, readable without signing in.
/// </summary>
/// <remarks>
/// <c>GET /me</c> already carries these for signed-in users, but the sign-up
/// screen needs them before anyone has an account. The alternative -- reading
/// the same environment variables again in the frontend -- would let the two
/// drift apart, which is exactly the bug a server-owned flag is meant to avoid.
/// Nothing here is sensitive: it only says which screens exist.
/// </remarks>
[ApiController]
[Route("features")]
[Produces("application/json")]
[AllowAnonymous]
public class FeaturesController(IOptions<FeatureOptions> featureOptions) : Controller
{
    [HttpGet]
    [ProducesResponseType<FeatureFlags>(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var features = featureOptions.Value;

        return Ok(new FeatureFlags(features.Organizations, features.PublicSignup));
    }
}
