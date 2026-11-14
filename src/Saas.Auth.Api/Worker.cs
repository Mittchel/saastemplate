using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using Saas.Auth.Api.Tenancy;
using Saas.Business.Services.Interfaces;
using Saas.Core.Constants;
using Saas.Core.Options;
using Saas.Data.Extensions;
using Saas.Data.Models;

namespace Saas.Auth.Api;

/// <summary>
/// Seeds the OAuth client, platform roles and -- in Development only -- a user
/// to sign in with.
/// </summary>
/// <remarks>
/// Schema migration is owned by the resource API's worker, so the two services
/// do not race to apply the same migrations on startup. This one assumes the
/// tables exist and retries while they do not.
/// </remarks>
public class Worker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IOptions<OpenIddictCertificateOptions> openIddictOptions,
    IHostEnvironment environment,
    ILogger<Worker> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        await WaitForSchemaAsync(scope.ServiceProvider, cancellationToken);
        await SeedClientsAsync(scope.ServiceProvider, cancellationToken);
        await SeedPlatformAdministratorAsync(scope.ServiceProvider);

        if (environment.IsDevelopment())
        {
            await SeedDevelopmentUserAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Both services start together and the resource API applies migrations, so
    /// the tables may not exist for the first few seconds.
    /// </summary>
    private async Task WaitForSchemaAsync(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var context = scope.GetRequiredService<Data.ApplicationDbContext>();

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT 1 FROM \"Users\" LIMIT 1", cancellationToken);
                return;
            }
            catch (Exception) when (attempt < 30)
            {
                if (attempt == 1)
                {
                    logger.LogInformation("Waiting for the database schema to be created...");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        logger.LogWarning("Database schema did not appear; seeding may fail.");
    }

    /// <summary>Registers the Next.js app as a confidential OAuth client.</summary>
    private async Task SeedClientsAsync(IServiceProvider scope, CancellationToken cancellationToken)
    {
        var manager = scope.GetRequiredService<IOpenIddictApplicationManager>();

        foreach (var client in openIddictOptions.Value.Clients.Where(c => !string.IsNullOrWhiteSpace(c.ClientId)))
        {
            if (await manager.FindByClientIdAsync(client.ClientId, cancellationToken) is not null)
            {
                continue;
            }

            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = client.ClientId,
                ClientSecret = client.ClientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                DisplayName = string.IsNullOrWhiteSpace(client.DisplayName)
                    ? client.ClientId
                    : client.DisplayName,
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.Prefixes.GrantType
                        + Controllers.AuthorizationController.ExternalGrantType,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "saas.api",
                },
            }, cancellationToken);

            logger.LogInformation("Seeded OAuth client {ClientId}", client.ClientId);
        }
    }

    /// <summary>
    /// Ensures the platform Administrator role exists and grants it to the
    /// configured operator account.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Earlier this seeded the role with a permission claim per declared
    /// permission. Those claims were never read: permissions are derived from
    /// the caller organization role when the token is minted, so the role
    /// claims were overwritten every time. The role is now a marker, and the
    /// <c>is_platform_admin</c> claim is what the API actually checks.
    /// </para>
    /// <para>
    /// Granting by email from configuration means a fresh deployment has an
    /// operator without a manual step, and re-running is harmless.
    /// </para>
    /// </remarks>
    private async Task SeedPlatformAdministratorAsync(IServiceProvider scope)
    {
        var roleManager = scope.GetRequiredService<RoleManager<RoleEntity>>();

        if (!await roleManager.RoleExistsAsync(PlatformRoles.Administrator))
        {
            await roleManager.CreateAsync(new RoleEntity
            {
                Name = PlatformRoles.Administrator,
                Description = "Operates the platform; can enter any workspace.",
            });

            logger.LogInformation("Seeded the {Role} role.", PlatformRoles.Administrator);
        }

        var email = configuration["Platform:AdministratorEmail"]?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogInformation(
                "No Platform:AdministratorEmail configured; no operator account granted.");
            return;
        }

        var userManager = scope.GetRequiredService<UserManager<UserEntity>>();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            // Not an error: the account may simply not have registered yet, and
            // the grant is applied on the next restart once it has.
            logger.LogWarning(
                "Platform administrator {Email} is configured but no such account exists yet.",
                email);
            return;
        }

        if (await userManager.IsInRoleAsync(user, PlatformRoles.Administrator))
        {
            return;
        }

        await userManager.AddToRoleAsync(user, PlatformRoles.Administrator);

        logger.LogWarning(
            "Granted platform administrator to {Email}.", email);
    }

    /// <summary>
    /// Creates the users to sign in with out of the box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two of them. <c>dev@saas.local</c> is normally also the configured
    /// platform operator, so signing in as it shows the app with the operator
    /// tools visible. <c>member@saas.local</c> is deliberately an ordinary
    /// account with its own workspace, which gives you something to look at the
    /// app as -- and gives the test suite a second tenant without registering
    /// one, which registration rate limiting would throttle.
    /// </para>
    /// <para>
    /// Development only, and the credentials are printed to the log rather than
    /// hidden in a comment, so there is no doubt that these accounts exist.
    /// </para>
    /// </remarks>
    private async Task SeedDevelopmentUserAsync(
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        await SeedUserAsync(scope, "dev@saas.local", "Dev", "User", cancellationToken);
        await SeedUserAsync(scope, "member@saas.local", "Member", "User", cancellationToken);
    }

    private async Task SeedUserAsync(
        IServiceProvider scope,
        string email,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        const string password = "DevPassword123";

        var userManager = scope.GetRequiredService<UserManager<UserEntity>>();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new UserEntity
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}",
        };

        // Same atomicity requirement as registration: a half-seeded user with no
        // workspace cannot sign in, and the seeder would skip it on the next
        // boot because the account already exists.
        var context = scope.GetRequiredService<Data.ApplicationDbContext>();

        await context.ExecuteInTransactionAsync(async ct =>
        {
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed the development user {email}: " +
                    string.Join(' ', created.Errors.Select(e => e.Description)));
            }

            var tenantContext = scope.GetRequiredService<MutableTenantContext>();
            tenantContext.SetUser(user.Id);

            var provisioning = scope.GetRequiredService<IProvisioningService>();
            await provisioning.CreatePersonalOrganizationAsync(user, ct);
        }, cancellationToken);

        logger.LogWarning(
            "Seeded development user {Email} with password {Password}. Development only.",
            email, password);
    }
}
