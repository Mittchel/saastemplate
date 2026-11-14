using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Saas.Auth.Api.Controllers;
using Saas.Auth.Api.Services;
using Saas.Auth.Api.Tenancy;
using Saas.Core.Options;
using Saas.Core.Tenancy;
using Saas.Data;
using Saas.Data.Models;

namespace Saas.Auth.Api.Extensions;

/// <summary>Identity-service registration.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ASP.NET Identity with Guid keys, matching the entity configuration.
    /// </summary>
    public static IServiceCollection AddSaasIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddIdentity<UserEntity, RoleEntity>(identity =>
            {
                identity.User.RequireUniqueEmail = true;
                identity.SignIn.RequireConfirmedAccount = false;

                identity.Password.RequiredLength = 10;
                identity.Password.RequireDigit = true;
                identity.Password.RequireUppercase = true;
                identity.Password.RequireNonAlphanumeric = false;

                identity.Lockout.MaxFailedAccessAttempts = 10;
                identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // OpenIddict matches tokens on these claim types.
        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
            options.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
            options.ClaimsIdentity.EmailClaimType = OpenIddictConstants.Claims.Email;
            options.ClaimsIdentity.RoleClaimType = OpenIddictConstants.Claims.Role;
        });

        services.Configure<ExternalLoginOptions>(
            configuration.GetSection(ExternalLoginOptions.SectionName));

        services.AddSingleton<IExternalIdentityValidator, ExternalIdentityValidator>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        services.AddScoped<MutableTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<MutableTenantContext>());

        return services;
    }

    /// <summary>
    /// The OpenIddict authorization server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Password and refresh flows only. The Next.js app renders the login form
    /// itself (shadcn login-03) and exchanges credentials here, rather than
    /// redirecting to a server-rendered page -- which is what keeps the login
    /// screen under the frontend's design control.
    /// </para>
    /// <para>
    /// <c>DisableAccessTokenEncryption</c> is mandatory, not stylistic: the
    /// Python AI service validates these tokens as ordinary JWTs via the JWKS
    /// endpoint, which is impossible if they arrive encrypted. Refresh tokens
    /// and authorization codes stay encrypted.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddOpenIddictServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var certificates = configuration
            .GetSection(OpenIddictCertificateOptions.SectionName)
            .Get<OpenIddictCertificateOptions>() ?? new OpenIddictCertificateOptions();

        services.Configure<OpenIddictCertificateOptions>(
            configuration.GetSection(OpenIddictCertificateOptions.SectionName));

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("connect/token")
                    .SetUserInfoEndpointUris("connect/userinfo");

                // Without this the iss claim follows the request host, so the
                // same token is valid through the gateway and rejected on the
                // published port (or vice versa).
                if (!string.IsNullOrWhiteSpace(certificates.Issuer))
                {
                    options.SetIssuer(certificates.Issuer);
                }

                options.AllowPasswordFlow()
                    .AllowRefreshTokenFlow()
                    // Social sign-in. A custom grant rather than a side-door
                    // endpoint, so tokens are still issued by OpenIddict with
                    // the same claims, lifetimes and rotation as every other
                    // sign-in path.
                    .AllowCustomFlow(AuthorizationController.ExternalGrantType);

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Roles,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    "saas.api");

                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(30))
                    .SetRefreshTokenLifetime(TimeSpan.FromDays(14));

                // The resource API and the AI service both validate signatures
                // from the JWKS endpoint, so this key must outlive a restart.
                // OpenIddict's development certificates are regenerated on every
                // boot and would invalidate every issued token.
                if (TryLoadCertificates(certificates, out var signing, out var encryption))
                {
                    options.AddSigningCertificate(signing!)
                        .AddEncryptionCertificate(encryption!);
                }
                else if (environment.IsDevelopment())
                {
                    options.AddDevelopmentSigningCertificate()
                        .AddDevelopmentEncryptionCertificate();
                }
                else
                {
                    throw new InvalidOperationException(
                        "OpenIddict signing and encryption certificates are required outside " +
                        "Development. Generate them with scripts/generate-certs.ps1 and set " +
                        "OpenIddict:SigningCertificatePath / :EncryptionCertificatePath.");
                }

                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();

                if (!environment.IsDevelopment())
                {
                    return;
                }

                options.UseAspNetCore().DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    private static bool TryLoadCertificates(
        OpenIddictCertificateOptions options,
        out X509Certificate2? signing,
        out X509Certificate2? encryption)
    {
        signing = null;
        encryption = null;

        if (!options.HasCertificates
            || !File.Exists(options.SigningCertificatePath)
            || !File.Exists(options.EncryptionCertificatePath))
        {
            return false;
        }

        signing = X509CertificateLoader.LoadPkcs12FromFile(
            options.SigningCertificatePath!,
            options.SigningCertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);

        encryption = X509CertificateLoader.LoadPkcs12FromFile(
            options.EncryptionCertificatePath!,
            options.EncryptionCertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);

        return true;
    }
}
