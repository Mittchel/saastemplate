using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Saas.Data;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>.
/// </summary>
/// <remarks>
/// Resolves the <em>owner</em> connection string, because migrations create and
/// alter tables and enable row-level security -- none of which the runtime
/// <c>saas_app</c> login is permitted to do. Reads
/// <c>ConnectionStrings__Migrations</c> from the environment so the same
/// command works on a laptop and in CI.
/// </remarks>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string LocalhostFallback =
        "Host=localhost;Port=5432;Database=saas;Username=saas_owner;Password=owner_change_me";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Migrations")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? LocalhostFallback;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        // No ITenantContext: migrations run outside any tenant, so the global
        // query filters evaluate against a null organization and match nothing.
        // That is intentional -- migrations should never read tenant data.
        return new ApplicationDbContext(options);
    }
}
