using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Data.Migrations;

/// <summary>
/// A maintenance function that fails runs left hanging.
/// </summary>
/// <remarks>
/// <para>
/// A run is marked <c>Running</c> before the model is called and
/// <c>Completed</c> afterwards. If the AI service dies mid-run -- or the call
/// to report the outcome is itself what failed -- the row stays
/// <c>Running</c> forever, and the user sees a conversation that never
/// resolves.
/// </para>
/// <para>
/// Sweeping those rows is inherently cross-tenant, which row-level security
/// exists to prevent: the policies apply to the table owner too, because they
/// are <c>FORCE</c>d, so no ordinary role can see every stale run. Rather than
/// weakening the policies or handing a background job a role that bypasses
/// them, the sweep is a <c>SECURITY DEFINER</c> function owned by a role that
/// already bypasses RLS. The privilege is bounded by the function body: it can
/// only move <c>Running</c> rows older than the cutoff to <c>Failed</c>, and it
/// returns a count rather than any tenant data.
/// </para>
/// </remarks>
public partial class StaleRunReaper : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION app_reap_stale_runs(older_than interval)
                RETURNS integer
                LANGUAGE plpgsql
                -- Runs as the function owner, which is how it sees across
                -- tenants. The body is the entire scope of that privilege.
                SECURITY DEFINER
                -- Pinned so the function cannot be redirected at a lookalike
                -- table by a caller-controlled search_path.
                SET search_path = public, pg_temp
                AS $$
            DECLARE
                affected integer;
            BEGIN
                UPDATE "Runs"
                SET "Status" = 3,
                    "Error" = COALESCE("Error",
                        'The run did not report a result and was timed out.'),
                    "CompletedAt" = now(),
                    "ModifiedDate" = now()
                WHERE "Status" = 1
                  AND COALESCE("StartedAt", "CreatedDate") < now() - older_than;

                GET DIAGNOSTICS affected = ROW_COUNT;

                RETURN affected;
            END;
            $$;

            DO $do$
            BEGIN
                -- Fail loudly rather than leave a sweep that silently finds
                -- nothing: owned by saas_owner the function is still subject to
                -- FORCEd row-level security and would never see another
                -- tenant's stale run.
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_maintenance') THEN
                    RAISE EXCEPTION 'Role saas_maintenance is missing. Create it as a superuser: CREATE ROLE saas_maintenance NOLOGIN BYPASSRLS; GRANT saas_maintenance TO saas_owner;';
                END IF;

                -- Reassigning ownership requires the incoming owner to hold
                -- CREATE on the schema, and this role is otherwise granted
                -- nothing at all.
                GRANT USAGE, CREATE ON SCHEMA public TO saas_maintenance;

                ALTER FUNCTION app_reap_stale_runs(interval) OWNER TO saas_maintenance;

                -- BYPASSRLS exempts the role from the policies, not from
                -- ordinary table privileges. Granted on this table alone, and
                -- only the two operations the sweep performs.
                GRANT SELECT, UPDATE ON "Runs" TO saas_maintenance;

                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app') THEN
                    GRANT EXECUTE ON FUNCTION app_reap_stale_runs(interval) TO saas_app;
                END IF;
            END
            $do$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS app_reap_stale_runs(interval);");
    }
}
