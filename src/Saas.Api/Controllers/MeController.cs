using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Business.Contracts;
using Saas.Business.Services.Interfaces;

namespace Saas.Api.Controllers;

/// <summary>The signed-in user.</summary>
[ApiController]
[Route("me")]
[Consumes("application/json")]
[Produces("application/json")]
[Authorize]
public class MeController(ILogger<MeController> logger, IUserService userService) : Controller
{
    /// <summary>
    /// Everything the frontend needs to render a signed-in shell: the user,
    /// their active organization, the organizations they can switch into, their
    /// permissions and the server's feature flags.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await userService.GetCurrentUserAsync(cancellationToken);

        logger.LogDebug("Resolved current user {UserId}", user.Id);

        return Ok(user);
    }

    /// <summary>Updates the signed-in user's own profile.</summary>
    [HttpPut]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await userService.UpdateProfileAsync(
            request.FirstName, request.LastName, cancellationToken));
    }
}

public record UpdateProfileRequest(string FirstName, string LastName);
