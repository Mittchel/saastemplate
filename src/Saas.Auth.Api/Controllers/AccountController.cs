using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Saas.Auth.Api.Tenancy;
using Saas.Business.Services.Interfaces;
using Saas.Core.Email;
using Saas.Core.Extensions;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;
using Saas.Core.Options;
using Saas.Data;
using Saas.Data.Extensions;
using Saas.Data.Models;

namespace Saas.Auth.Api.Controllers;

/// <summary>Registration and password management.</summary>
[ApiController]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
[Route("account")]
[Consumes("application/json")]
[Produces("application/json")]
public class AccountController(
    ILogger<AccountController> logger,
    UserManager<UserEntity> userManager,
    IProvisioningService provisioningService,
    MutableTenantContext tenantContext,
    ApplicationDbContext context,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    IOptions<FeatureOptions> featureOptions) : Controller
{
    private readonly FeatureOptions _features = featureOptions.Value;

    /// <summary>
    /// Registers a user and provisions their personal workspace.
    /// </summary>
    /// <remarks>
    /// The workspace is created whether or not the Organizations feature is
    /// enabled: the tenant is always an organization, so there is one code path
    /// and switching the feature on later needs no migration.
    /// </remarks>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.SensitivePolicy)]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (!_features.PublicSignup)
        {
            throw FunctionalException.Forbidden(
                "Self-service signup is disabled; ask for an invitation.",
                ErrorScenario.OrganizationsDisabled);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw FunctionalException.Conflict(
                "An account with this email address already exists.",
                ErrorScenario.EmailAlreadyRegistered);
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        var user = new UserEntity
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            FullName = string.Join(' ', new[] { firstName, lastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))),
        };

        // Atomic on purpose. A failure between creating the account and
        // provisioning its workspace would leave a user who cannot sign in at
        // all -- the password grant rejects an account with no organization --
        // and no self-service way to recover. The transaction helper is
        // re-entrant, so provisioning joins this one instead of nesting.
        OrganizationEntity organization = null!;

        await context.ExecuteInTransactionAsync(async ct =>
        {
            var created = await userManager.CreateAsync(user, request.Password);

            if (!created.Succeeded)
            {
                throw new FunctionalException(
                    string.Join(' ', created.Errors.Select(e => e.Description)),
                    ErrorScenario.PasswordTooWeak);
            }

            // Provisioning writes a membership row, whose RLS policy reads
            // app.user_id -- publish the new identity before provisioning.
            tenantContext.SetUser(user.Id);

            organization = await provisioningService.CreatePersonalOrganizationAsync(user, ct);
        }, cancellationToken);

        logger.LogInformation(
            "Registered {UserId} with personal organization {OrganizationId}",
            user.Id, organization.Id);

        return Created(
            $"/account/{user.Id}",
            new RegisterResponse(user.Id, email, organization.Id, organization.Name));
    }

    /// <summary>
    /// Starts a password reset.
    /// </summary>
    /// <remarks>
    /// Always returns 204, whether or not the address is registered. Reporting
    /// "no such account" would turn this endpoint into a way to discover who
    /// has one, which is exactly what the login form is careful not to do.
    /// </remarks>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.SensitivePolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            // URL-safe: the raw token contains characters that do not survive a
            // query string intact.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var link = $"{appOptions.Value.WebUrl}/reset-password" +
                       $"?email={Uri.EscapeDataString(email)}&token={encoded}";

            await emailSender.SendAsync(
                email,
                "Reset your password",
                $"""
                 Someone asked to reset the password for this account.

                 {link}

                 The link is valid for a short time and can be used once. If this
                 was not you, nothing has changed and you can ignore this email.
                 """,
                cancellationToken);
        }
        else
        {
            logger.LogInformation(
                "Password reset requested for an unregistered address.");
        }

        return NoContent();
    }

    /// <summary>Completes a password reset.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());

        // A bad token and an unknown address give the same answer, for the same
        // reason as above.
        if (user is null)
        {
            throw new FunctionalException(
                "This reset link is no longer valid. Request a new one.",
                ErrorScenario.ValidationFailed);
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            throw new FunctionalException(
                "This reset link is no longer valid. Request a new one.",
                ErrorScenario.ValidationFailed);
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);

        if (!result.Succeeded)
        {
            // Identity reports both "wrong token" and "weak password" here; the
            // password rules are safe to echo, the token state is not.
            var passwordProblems = result.Errors
                .Where(e => e.Code.Contains("Password", StringComparison.Ordinal))
                .Select(e => e.Description)
                .ToList();

            throw new FunctionalException(
                passwordProblems.Count > 0
                    ? string.Join(' ', passwordProblems)
                    : "This reset link is no longer valid. Request a new one.",
                passwordProblems.Count > 0
                    ? ErrorScenario.PasswordTooWeak
                    : ErrorScenario.ValidationFailed);
        }

        logger.LogInformation("Password reset completed for {UserId}", user.Id);

        return NoContent();
    }

    /// <summary>Changes the signed-in user's password.</summary>
    [HttpPost("password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User)
            ?? throw FunctionalException.NotFound("User");

        var result = await userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new FunctionalException(
                string.Join(' ', result.Errors.Select(e => e.Description)),
                ErrorScenario.ValidationFailed);
        }

        return NoContent();
    }
}

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);

public record RegisterResponse(Guid UserId, string Email, Guid OrganizationId, string OrganizationName);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
