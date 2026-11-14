using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saas.Data.Interceptors;

namespace Saas.Data.Extensions;

/// <summary>
/// Data-layer registration, following the per-layer extension convention
/// (<c>AddData</c> / <c>AddBusiness</c>) rather than inlining everything in
/// each service's startup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the context against the runtime connection string.
    /// </summary>
    /// <remarks>
    /// Uses <c>DefaultConnection</c>, which points at the non-owner
    /// <c>saas_app</c> login so row-level security applies. Migrations run
    /// separately against <c>Migrations</c> as the table owner -- see
    /// <see cref="ApplicationDbContextFactory"/>.
    /// </remarks>
    public static IServiceCollection AddData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured.");

        services.AddScoped<TenantConnectionInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            });

            // Publishes app.organization_id / app.user_id onto every connection
            // this context opens, which is what RLS policies read.
            options.AddInterceptors(serviceProvider.GetRequiredService<TenantConnectionInterceptor>());
        });

        // OpenIddict stores its applications, authorizations and tokens in the
        // same context; the identity service configures the rest.
        services.AddOpenIddict()
            .AddCore(builder => builder
                .UseEntityFrameworkCore()
                .UseDbContext<ApplicationDbContext>());

        return services;
    }
}
