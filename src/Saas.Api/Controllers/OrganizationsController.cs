using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Business.Contracts;
using Saas.Business.Services.Interfaces;
using Saas.Data.Models;
using Saas.Data.Models.Constants;

namespace Saas.Api.Controllers;

/// <summary>
/// Organizations, their members and their pending invitations.
/// </summary>
/// <remarks>
/// Every route here requires the Organizations feature except
/// <see cref="List"/>, which the organization switcher needs regardless -- with
/// the feature off a user still has exactly one (personal) organization, and
/// the UI simply does not render a switcher for it.
/// </remarks>
[ApiController]
[Route("organizations")]
[Consumes("application/json")]
[Produces("application/json")]
[Authorize]
public class OrganizationsController(
    IOrganizationService organizationService,
    IMembershipService membershipService,
    IInvitationService invitationService) : Controller
{
    /// <summary>Organizations the signed-in user can switch into.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.OrganizationRead)]
    [ProducesResponseType<IReadOnlyList<OrganizationSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await organizationService.ListAsync(cancellationToken));

    [HttpGet("{organizationId:guid}")]
    [Authorize(Policy = Permissions.OrganizationRead)]
    [ProducesResponseType<OrganizationDetail>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken cancellationToken) =>
        Ok(await organizationService.GetAsync(organizationId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Permissions.OrganizationWrite)]
    [ProducesResponseType<OrganizationSummary>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var organization = await organizationService.CreateAsync(request.Name, cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { organizationId = organization.Id },
            organization);
    }

    [HttpPut("{organizationId:guid}")]
    [Authorize(Policy = Permissions.OrganizationWrite)]
    [ProducesResponseType<OrganizationDetail>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await organizationService.UpdateAsync(organizationId, request.Name, cancellationToken));

    [HttpDelete("{organizationId:guid}")]
    [Authorize(Policy = Permissions.OrganizationDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid organizationId, CancellationToken cancellationToken)
    {
        await organizationService.DeleteAsync(organizationId, cancellationToken);

        return NoContent();
    }

    // --- Members -----------------------------------------------------------

    [HttpGet("{organizationId:guid}/members")]
    [Authorize(Policy = Permissions.MemberRead)]
    [ProducesResponseType<IReadOnlyList<MemberSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembers(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        Ok(await membershipService.ListAsync(organizationId, cancellationToken));

    [HttpPut("{organizationId:guid}/members/{userId:guid}")]
    [Authorize(Policy = Permissions.MemberWrite)]
    [ProducesResponseType<MemberSummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMemberRole(
        Guid organizationId,
        Guid userId,
        [FromBody] UpdateMemberRoleRequest request,
        CancellationToken cancellationToken) =>
        Ok(await membershipService.UpdateRoleAsync(
            organizationId, userId, request.Role, cancellationToken));

    [HttpDelete("{organizationId:guid}/members/{userId:guid}")]
    [Authorize(Policy = Permissions.MemberRemove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await membershipService.RemoveAsync(organizationId, userId, cancellationToken);

        return NoContent();
    }

    /// <summary>Leaves an organization the signed-in user joined.</summary>
    [HttpPost("{organizationId:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid organizationId, CancellationToken cancellationToken)
    {
        await membershipService.LeaveAsync(organizationId, cancellationToken);

        return NoContent();
    }

    // --- Invitations -------------------------------------------------------

    [HttpGet("{organizationId:guid}/invitations")]
    [Authorize(Policy = Permissions.InvitationManage)]
    [ProducesResponseType<IReadOnlyList<InvitationSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvitations(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        Ok(await invitationService.ListAsync(organizationId, cancellationToken));

    /// <summary>
    /// Creates an invitation. The response includes the token so the caller can
    /// build the link; wire an email sender here for production.
    /// </summary>
    [HttpPost("{organizationId:guid}/invitations")]
    [Authorize(Policy = Permissions.InvitationManage)]
    [ProducesResponseType<CreateInvitationResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvitation(
        Guid organizationId,
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var (invitation, token) = await invitationService.CreateAsync(
            organizationId, request.Email, request.Role, cancellationToken);

        return CreatedAtAction(
            nameof(ListInvitations),
            new { organizationId },
            new CreateInvitationResponse(invitation, token));
    }

    [HttpDelete("{organizationId:guid}/invitations/{invitationId:guid}")]
    [Authorize(Policy = Permissions.InvitationManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeInvitation(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await invitationService.RevokeAsync(organizationId, invitationId, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Redeems an invitation for the signed-in user.
    /// </summary>
    /// <remarks>
    /// Deliberately not nested under an organization id: the caller is not yet
    /// a member of one, and the token identifies the target.
    /// </remarks>
    [HttpPost("/invitations/accept")]
    [ProducesResponseType<OrganizationSummary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken) =>
        Ok(await invitationService.AcceptAsync(request.Token, cancellationToken));
}

public record CreateInvitationResponse(InvitationSummary Invitation, string Token);
