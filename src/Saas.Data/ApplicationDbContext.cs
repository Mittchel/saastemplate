using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Saas.Core.Tenancy;
using Saas.Data.Models;

namespace Saas.Data;

/// <summary>
/// The single database context, shared by the identity service and the
/// resource API.
/// </summary>
/// <remarks>
/// <para>
/// Tenant isolation is enforced twice. Layer one is the global query filters
/// configured here, which give ordinary application code sane behaviour. Layer
/// two is Postgres row-level security, applied in a migration and enforced
/// against the non-owner <c>saas_app</c> login. Layer one keeps bugs cheap;
/// layer two keeps them from becoming data breaches.
/// </para>
/// <para>
/// <see cref="OrganizationEntity"/> itself carries no query filter: a filter
/// there would need a correlated subquery over memberships on every load and
/// would trip the required-navigation warnings from every tenant-scoped entity
/// that points at it. Organizations are reached through membership joins in the
/// service layer, and RLS still covers direct access.
/// </para>
/// </remarks>
public class ApplicationDbContext : IdentityDbContext<UserEntity, RoleEntity, Guid>
{
    private readonly ITenantContext? _tenantContext;

    /// <summary>Design-time and migration constructor; no tenant in scope.</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Referenced by the global query filters. Reading it through a property on
    /// the context instance is what makes EF treat it as a per-query parameter
    /// rather than baking the first request's value into the compiled model.
    /// </summary>
    public Guid? CurrentOrganizationId => _tenantContext?.OrganizationId;

    public Guid? CurrentUserId => _tenantContext?.UserId;

    public virtual DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public virtual DbSet<MembershipEntity> Memberships => Set<MembershipEntity>();
    public virtual DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();
    public virtual DbSet<RunEntity> Runs => Set<RunEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();

        ConfigureIdentityTableNames(builder);
        ConfigureOrganizations(builder);
        ConfigureRuns(builder);
        ConfigureTenantQueryFilters(builder);

        // Database clock, not application clock, so CreatedDate stays monotonic
        // across services and timezones.
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            builder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.CreatedDate))
                .HasDefaultValueSql("now()")
                .ValueGeneratedOnAdd();
        }
    }

    /// <summary>Plain plural table names instead of the AspNet* defaults.</summary>
    private static void ConfigureIdentityTableNames(ModelBuilder builder)
    {
        builder.Entity<UserEntity>().ToTable("Users");
        builder.Entity<RoleEntity>().ToTable("Roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");

        builder.Entity<UserEntity>()
            .Property(u => u.CreatedDate)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();
    }

    private static void ConfigureOrganizations(ModelBuilder builder)
    {
        builder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("Organizations");
            entity.Property(o => o.Name).HasMaxLength(200).IsRequired();
            entity.Property(o => o.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(o => o.Slug).IsUnique();
        });

        builder.Entity<MembershipEntity>(entity =>
        {
            entity.ToTable("Memberships");

            entity.HasOne(m => m.Organization)
                .WithMany(o => o.Memberships)
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One membership per user per organization.
            entity.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
            entity.HasIndex(m => m.UserId);
        });

        builder.Entity<InvitationEntity>(entity =>
        {
            entity.ToTable("Invitations");
            entity.Property(i => i.Email).HasMaxLength(320).IsRequired();
            entity.Property(i => i.Token).HasMaxLength(128).IsRequired();
            entity.HasIndex(i => i.Token).IsUnique();
            entity.HasIndex(i => new { i.OrganizationId, i.Email });

            entity.HasOne(i => i.Organization)
                .WithMany(o => o.Invitations)
                .HasForeignKey(i => i.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRuns(ModelBuilder builder)
    {
        builder.Entity<RunEntity>(entity =>
        {
            entity.ToTable("Runs");
            entity.Property(r => r.ThreadId).HasMaxLength(128).IsRequired();
            entity.Property(r => r.Model).HasMaxLength(100).IsRequired();

            entity.HasOne(r => r.Organization)
                .WithMany()
                .HasForeignKey(r => r.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(r => new { r.OrganizationId, r.CreatedDate });
            entity.HasIndex(r => r.ThreadId);
        });
    }

    private void ConfigureTenantQueryFilters(ModelBuilder builder)
    {
        // Memberships are intentionally wider: a row is visible when it belongs
        // to the active organization OR to the calling user. The second clause
        // is what lets the organization switcher enumerate the organizations a
        // user may switch into. The RLS policy mirrors this exactly.
        builder.Entity<MembershipEntity>()
            .HasQueryFilter(m =>
                m.OrganizationId == CurrentOrganizationId || m.UserId == CurrentUserId);

        builder.Entity<InvitationEntity>()
            .HasQueryFilter(i => i.OrganizationId == CurrentOrganizationId);


        builder.Entity<RunEntity>()
            .HasQueryFilter(r => r.OrganizationId == CurrentOrganizationId);

    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenantAndTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampTenantAndTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Fills in <c>OrganizationId</c> on new tenant-scoped rows and maintains
    /// <c>ModifiedDate</c>, so callers never have to remember either.
    /// </summary>
    private void StampTenantAndTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedDate = DateTimeOffset.UtcNow;
            }

            if (entry is { State: EntityState.Added, Entity: ITenantScoped scoped }
                && scoped.OrganizationId == Guid.Empty
                && CurrentOrganizationId is { } organizationId)
            {
                scoped.OrganizationId = organizationId;
            }
        }
    }
}
