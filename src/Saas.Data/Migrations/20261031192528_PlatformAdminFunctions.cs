using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Data.Migrations;

/// <summary>
/// The elevated reads a platform operator needs.
/// </summary>
/// <remarks>
/// <para>
/// Listing every tenant is exactly what row-level security exists to prevent,
/// and the policies are <c>FORCE</c>d so no ordinary login -- not even the table
/// owner -- can do it. Rather than adding an "or is an admin" escape clause to
/// every policy, which would move the guarantee from "the database cannot be
/// wrong" to "this token claim must be right", the operator reads go through a
/// short list of <c>SECURITY DEFINER</c> functions owned by
/// <c>saas_maintenance</c>: a role with <c>BYPASSRLS</c> and no <c>LOGIN</c>.
/// </para>
/// <para>
/// The privilege is bounded by the function bodies. They return summary rows
/// only -- counts and names -- and nothing here can write. Tenant data proper
/// stays unreachable: an operator who wants to see a customer's runs must
/// switch into that workspace, at which point they are genuinely scoped to it
/// and every normal policy applies.
/// </para>
/// </remarks>
public partial class PlatformAdminFunctions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Does this organization exist at all? Needed because an operator
            -- switching into a workspace is by definition not a member, so the
            -- ordinary lookup returns nothing and cannot tell "no such tenant"
            -- apart from "not yours".
            CREATE OR REPLACE FUNCTION app_organization_exists(organization uuid)
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = public, pg_temp
                AS $$ SELECT EXISTS (SELECT 1 FROM "Organizations" WHERE "Id" = organization) $$;

            -- One row per tenant, for the operator overview. Summary columns
            -- only: no run inputs, no invitation tokens, nothing a customer
            -- would consider their content.
            CREATE OR REPLACE FUNCTION app_list_organizations()
                RETURNS TABLE (
                    id uuid,
                    name text,
                    slug text,
                    is_personal boolean,
                    created_date timestamptz,
                    member_count bigint,
                    run_count bigint
                )
                LANGUAGE sql
                STABLE
                SECURITY DEFINER
                SET search_path = public, pg_temp
                AS $$
                    SELECT o."Id",
                           o."Name",
                           o."Slug",
                           o."IsPersonal",
                           o."CreatedDate",
                           (SELECT count(*) FROM "Memberships" m WHERE m."OrganizationId" = o."Id"),
                           (SELECT count(*) FROM "Runs" r WHERE r."OrganizationId" = o."Id")
                    FROM "Organizations" o
                    ORDER BY o."CreatedDate" DESC
                $$;
            """);

        migrationBuilder.Sql("""
            DO $do$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_maintenance') THEN
                    RAISE EXCEPTION 'Role saas_maintenance is missing. Create it as a superuser: CREATE ROLE saas_maintenance NOLOGIN BYPASSRLS; GRANT saas_maintenance TO saas_owner;';
                END IF;

                ALTER FUNCTION app_organization_exists(uuid) OWNER TO saas_maintenance;
                ALTER FUNCTION app_list_organizations() OWNER TO saas_maintenance;

                -- BYPASSRLS exempts the role from policies, not from table
                -- grants. Read-only, and only the tables these functions touch.
                GRANT SELECT ON "Organizations", "Memberships" TO saas_maintenance;

                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app') THEN
                    GRANT EXECUTE ON FUNCTION app_organization_exists(uuid) TO saas_app;
                    GRANT EXECUTE ON FUNCTION app_list_organizations() TO saas_app;
                END IF;
            END
            $do$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS app_list_organizations();
            DROP FUNCTION IF EXISTS app_organization_exists(uuid);
            """);
    }
}
