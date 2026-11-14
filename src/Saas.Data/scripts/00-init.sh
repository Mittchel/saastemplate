#!/bin/bash
# ---------------------------------------------------------------------------
# Runs ONCE, on first boot of an empty Postgres data directory.
#
# Creates three logins with deliberately different privileges:
#
#   saas_owner  owns the business tables and runs EF Core migrations.
#   saas_app    the API's runtime connection. NOT the table owner, which is
#               precisely why row-level security applies to it. This is the
#               whole point of splitting the roles -- a table owner bypasses
#               RLS unless FORCE ROW LEVEL SECURITY is set, and relying on
#               FORCE alone leaves no margin for error.
#   saas_ai     owns the `ai` schema (LangGraph checkpoints) and has no access
#               at all to the business tables. The AI service reaches business
#               data only through the .NET API, never through SQL.
# ---------------------------------------------------------------------------
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
    CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

    CREATE ROLE saas_owner LOGIN PASSWORD '${SAAS_OWNER_PASSWORD}';

    -- Maintenance identity. No LOGIN: nothing connects as this role. It exists
    -- only to own the SECURITY DEFINER sweep function, which has to see across
    -- tenants -- and row-level security is FORCEd, so even saas_owner cannot.
    -- Confining BYPASSRLS to a role that cannot log in keeps the exemption to
    -- the body of one audited function.
    CREATE ROLE saas_maintenance NOLOGIN BYPASSRLS;
    GRANT saas_maintenance TO saas_owner;
    CREATE ROLE saas_app   LOGIN PASSWORD '${SAAS_APP_PASSWORD}';
    CREATE ROLE saas_ai    LOGIN PASSWORD '${SAAS_AI_PASSWORD}';

    -- Business schema: owned by saas_owner, used by saas_app under RLS.
    ALTER SCHEMA public OWNER TO saas_owner;
    GRANT USAGE ON SCHEMA public TO saas_app;

    -- Migrations create tables later, so grant by default privilege rather
    -- than enumerating tables that do not exist yet.
    ALTER DEFAULT PRIVILEGES FOR ROLE saas_owner IN SCHEMA public
        GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO saas_app;
    ALTER DEFAULT PRIVILEGES FOR ROLE saas_owner IN SCHEMA public
        GRANT USAGE, SELECT ON SEQUENCES TO saas_app;

    -- AI schema: isolated. saas_app cannot read checkpoints, saas_ai cannot
    -- read business tables.
    CREATE SCHEMA ai AUTHORIZATION saas_ai;

    -- The LangGraph checkpointer creates its tables without naming a schema,
    -- so it would target public -- which saas_ai deliberately cannot write to.
    -- Pinning the search path keeps that default inside its own schema.
    ALTER ROLE saas_ai SET search_path = ai;
    GRANT USAGE ON SCHEMA public TO saas_ai;
    GRANT SELECT ON pg_catalog.pg_extension TO saas_ai;
EOSQL

echo "00-init.sh: roles saas_owner / saas_app / saas_ai created; schema 'ai' ready."
