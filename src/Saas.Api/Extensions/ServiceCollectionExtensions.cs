using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Saas.Api.Authorization;
using Saas.Api.Tenancy;
using Saas.Core.Constants;
using Saas.Core.Options;
using Saas.Core.Tenancy;
using Saas.Data.Models.Constants;
using AuthenticationOptions = Saas.Core.Options.AuthenticationOptions;

namespace Saas.Api.Extensions;

/// <summary>API-layer registration.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Validates access tokens minted by the identity service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Authority</c> and the expected issuer differ on purpose: metadata is
    /// fetched over the container network (<c>http://auth:8080</c>) while the
    /// <c>iss</c> claim carries the public hostname tokens were minted behind.
    /// Validating the issuer against the internal address would reject every
    /// token.
    /// </para>
    /// <para>
    /// <c>MapInboundClaims = false</c> keeps JWT claim names as issued, so
    /// <c>sub</c> stays <c>sub</c> instead of being silently rewritten to the
    /// long WS-Federation URI. The tenant context reads the raw names.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        services.Configure<AuthenticationOptions>(
            configuration.GetSection(AuthenticationOptions.SectionName));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.Authority = options.Authority;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.MapInboundClaims = false;

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,

                    // Both spellings of each authority. OpenIddict issues `iss`
                    // with a trailing slash, the configured URLs are written
                    // without one, and the comparison is ordinal -- so omitting
                    // either variant rejects every token with a bare 401.
                    ValidIssuers = IssuerVariants(options),

                    // OpenIddict scopes tokens by resource rather than a single
                    // audience; authorization is enforced per request from the membership row
                    // below, which is the check that actually matters here.
                    ValidateAudience = false,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };
            });

        return services;
    }

    /// <summary>
    /// Each configured authority with and without a trailing slash.
    /// </summary>
    private static string[] IssuerVariants(AuthenticationOptions options) =>
        new[] { options.PublicAuthority, options.Authority }
            .Where(authority => !string.IsNullOrWhiteSpace(authority))
            .SelectMany(authority => new[] { authority.TrimEnd('/'), authority.TrimEnd('/') + "/" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Registers one policy per declared permission.
    /// </summary>
    /// <remarks>
    /// Driven by reflection over <see cref="Permissions"/>, so adding a
    /// permission constant is the only step required to be able to
    /// <c>[Authorize(Policy = Permissions.X)]</c> against it.
    /// </remarks>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        var authorization = services.AddAuthorizationBuilder();

        foreach (var permission in Permissions.GetAll())
        {
            authorization.AddPolicy(permission, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new PermissionRequirement(permission));
            });
        }

        // Operator access. A separate axis from the per-organization
        // permissions above: the claim is minted only by the identity service,
        // only for an account in the Administrator role.
        authorization.AddPolicy(PlatformPolicies.Administrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(SaasClaimTypes.PlatformAdmin, "true");
        });

        // Anything not explicitly attributed still needs a signed-in caller
        // carrying an organization, so a forgotten [Authorize] fails closed.
        authorization.SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(SaasClaimTypes.OrganizationId)
            .Build());

        return services;
    }

    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        return services;
    }

    public static IServiceCollection AddSwaggerGenerator(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(swagger =>
        {
            swagger.SwaggerDoc("v1", new OpenApiInfo { Title = "Saas API", Version = "v1" });

            swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token from /connect/token.",
            });

            // OpenAPI.NET v2 (pulled in by Swashbuckle 10) replaced the inline
            // OpenApiReference with dedicated reference types, and takes the
            // requirement as a factory so the reference can be bound to the
            // document it belongs to.
            swagger.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() },
            });
        });

        return services;
    }

    /// <summary>
    /// Allows the Next.js app to call the API directly from the browser.
    /// </summary>
    /// <remarks>
    /// Origins come from configuration rather than a wildcard: the frontend
    /// sends bearer tokens, and a permissive policy would let any site do the
    /// same from a victim's browser.
    /// </remarks>
    public static IServiceCollection AddWebCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://app.localhost", "http://localhost:3000"];

        services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}
