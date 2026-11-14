using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Data.Migrations;

/// <summary>
/// The second tenant-isolation layer.
/// </summary>
/// <remarks>
/// <para>
/// The EF Core global query filters in <c>ApplicationDbContext</c> are layer
/// one: they make ordinary application code behave correctly. These policies
/// are layer two: they make a <em>missed</em> filter return zero rows rather
/// than another tenant's data. Layer one keeps bugs cheap, layer two keeps them
/// from becoming breaches.
/// </para>
/// <para>
/// Effective only because the runtime connects as <c>saas_app</c>, which does
/// not own these tables. <c>FORCE</c> is set as well so the policies still
/// apply if a future deployment gets the roles wrong.
/// </para>
/// <para>
/// Consequence worth knowing: a genuine cross-tenant data migration must
/// temporarily lift the force flag, for example
/// <c>ALTER TABLE "Runs" NO FORCE ROW LEVEL SECURITY;</c>, and restore it
/// afterwards.
/// </para>
/// </remarks>
public partial class RowLevelSecurity : Migration
{
    /// <summary>Tenant tables whose visibility is exactly "belongs to the active organization".</summary>
    private static readonly string[] StrictlyScopedTables =
    [
        "Invitations",
        "Subscriptions",
        "Runs",
        "UsageEvents",
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // current_setting(..., true) returns NULL instead of raising when the
        // variable has never been set, which is the case for unauthenticated
        // requests. NULLIF maps the empty string the interceptor writes for
        // "no tenant" onto NULL too, so every comparison below simply fails to
        // match rather than erroring.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION app_current_org() RETURNS uuid
                LANGUAGE sql STABLE
                AS $$ SELECT NULLIF(current_setting('app.organization_id', true), '')::uuid $$;

            CREATE OR REPLACE FUNCTION app_current_user_id() RETURNS uuid
                LANGUAGE sql STABLE
                AS $$ SELECT NULLIF(current_setting('app.user_id', true), '')::uuid $$;
            """);

        foreach (var table in StrictlyScopedTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;

                CREATE POLICY tenant_isolation ON "{table}"
                    USING ("OrganizationId" = app_current_org())
                    WITH CHECK ("OrganizationId" = app_current_org());
                """);
        }

        // Memberships deliberately allow reading your own rows from any
        // organization: without that clause the organization switcher could not
        // enumerate the organizations a user may switch into. Writes stay
        // confined to the active organization, because a forged membership row
        // is the one thing that would grant cross-tenant access -- bootstrap
        // paths widen it per transaction via TenantSession instead.
        migrationBuilder.Sql("""
            ALTER TABLE "Memberships" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "Memberships" FORCE ROW LEVEL SECURITY;

            CREATE POLICY membership_select ON "Memberships" FOR SELECT
                USING ("OrganizationId" = app_current_org() OR "UserId" = app_current_user_id());

            CREATE POLICY membership_insert ON "Memberships" FOR INSERT
                WITH CHECK ("OrganizationId" = app_current_org());

            CREATE POLICY membership_update ON "Memberships" FOR UPDATE
                USING ("OrganizationId" = app_current_org())
                WITH CHECK ("OrganizationId" = app_current_org());

            CREATE POLICY membership_delete ON "Memberships" FOR DELETE
                USING ("OrganizationId" = app_current_org());
            """);

        // Organizations are visible to their members. INSERT is unrestricted on
        // purpose: a freshly created organization cannot reference itself as the
        // active tenant, and a row nobody holds a membership for is unreachable
        // anyway. The accompanying membership insert is the guarded step.
        migrationBuilder.Sql("""
            ALTER TABLE "Organizations" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "Organizations" FORCE ROW LEVEL SECURITY;

            CREATE POLICY organization_select ON "Organizations" FOR SELECT
                USING (
                    "Id" = app_current_org()
                    OR EXISTS (
                        SELECT 1 FROM "Memberships" m
                        WHERE m."OrganizationId" = "Organizations"."Id"
                          AND m."UserId" = app_current_user_id()
                    )
                );

            CREATE POLICY organization_insert ON "Organizations" FOR INSERT
                WITH CHECK (true);

            CREATE POLICY organization_update ON "Organizations" FOR UPDATE
                USING ("Id" = app_current_org())
                WITH CHECK ("Id" = app_current_org());

            CREATE POLICY organization_delete ON "Organizations" FOR DELETE
                USING ("Id" = app_current_org());
            """);

        // Grants are normally inherited from ALTER DEFAULT PRIVILEGES in
        // scripts/00-init.sh, but restating them keeps the migration correct
        // when it is applied to a database that was provisioned by hand.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app') THEN
                    GRANT USAGE ON SCHEMA public TO saas_app;
                    GRANT SELECT, INSERT, UPDATE, DELETE
                        ON ALL TABLES IN SCHEMA public TO saas_app;
                    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO saas_app;
                    GRANT EXECUTE ON FUNCTION app_current_org() TO saas_app;
                    GRANT EXECUTE ON FUNCTION app_current_user_id() TO saas_app;
                END IF;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var table in StrictlyScopedTables)
        {
            migrationBuilder.Sql($"""
                DROP POLICY IF EXISTS tenant_isolation ON "{table}";
                ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE "{table}" DISABLE ROW LEVEL SECURITY;
                """);
        }

        migrationBuilder.Sql("""
            DROP POLICY IF EXISTS membership_select ON "Memberships";
            DROP POLICY IF EXISTS membership_insert ON "Memberships";
            DROP POLICY IF EXISTS membership_update ON "Memberships";
            DROP POLICY IF EXISTS membership_delete ON "Memberships";
            ALTER TABLE "Memberships" NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE "Memberships" DISABLE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS organization_select ON "Organizations";
            DROP POLICY IF EXISTS organization_insert ON "Organizations";
            DROP POLICY IF EXISTS organization_update ON "Organizations";
            DROP POLICY IF EXISTS organization_delete ON "Organizations";
            ALTER TABLE "Organizations" NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE "Organizations" DISABLE ROW LEVEL SECURITY;

            DROP FUNCTION IF EXISTS app_current_org();
            DROP FUNCTION IF EXISTS app_current_user_id();
            """);
    }
}
