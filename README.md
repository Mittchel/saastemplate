# AI SaaS Template

A multi-tenant, AI-enabled SaaS starter: .NET for identity and the API, Python
for the agent, Next.js for the UI, Postgres underneath, all in containers. Clone
it, rename it, and start writing product code.

```
docker compose up -d
```

Then open <http://app.localhost> and sign in as `dev@saas.local` /
`DevPassword123`. A second account, `member@saas.local`, is seeded with the same
password as an ordinary user with its own workspace -- useful for seeing the app
as a customer rather than as the operator. Both are Development only.

## What you get

- **Authentication** — OpenIddict issuing JWTs, Auth.js holding them in an
  httpOnly cookie, shadcn `login-03` as the sign-in screen. Email and password,
  optional Google sign-in, and a working password-reset flow.
- **Organizations** — every user gets a personal workspace; teams, roles,
  invitations and switching are a config flag away.
- **Multi-tenancy** — enforced twice: EF Core query filters *and* Postgres
  row-level security.
- **A streaming agent** — LangGraph over SSE, with a tool that calls the .NET
  API as the signed-in user. OpenAI or Anthropic, switched by one variable.
- **A platform operator** — one account that can see every workspace and enter
  any of them for support, without being a member of any.
- **An app shell** — shadcn `sidebar-07`: collapsible icon sidebar, workspace
  switcher, and the signed-in user bottom-left with sign-out.
- **The operational bits** — per-IP rate limiting, a sweep that fails runs left
  hanging, CI, and a production compose overlay with automatic HTTPS.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Docker Desktop | 29+ | The only hard requirement to *run* the stack. |
| .NET SDK | 10.0.1xx | To build or test the .NET services locally. |
| Node.js | 20.18+ | To run the frontend outside Docker. |
| Python | — | Not needed; the AI service only ever runs in a container. |
| openssl | any | Ships with Git for Windows. Used once, for certificates. |

## First run

```bash
cp .env.example .env
# Generate the token signing certificates (they must survive restarts).
./scripts/generate-certs.ps1 -Password <a-password>
# Put that password in .env as OIDC_SIGNING_CERT_PASSWORD / OIDC_ENCRYPTION_CERT_PASSWORD,
# and set OPENAI_API_KEY (or ANTHROPIC_API_KEY) so the agent can answer.
docker compose up -d
```

The API applies database migrations on startup; the identity service seeds the
OAuth client, the platform `Administrator` role and the two development
accounts.

### Where things listen

| Service | Through Traefik | Direct (dev only) |
|---|---|---|
| Web | <http://app.localhost> | <http://localhost:3000> |
| API | <http://api.localhost> | <http://localhost:5002> |
| Identity | <http://auth.localhost> | <http://localhost:5001> |
| AI | <http://api.localhost/ai> | <http://localhost:5003> |
| Mailpit | — | <http://localhost:8025> |
| Postgres | — | `localhost:15432` |

Browsers resolve `*.localhost` themselves; Windows DNS does not, which is why
the direct ports exist for `curl` and `psql`. Postgres and Redis are on
non-default ports on purpose, because a machine running several of these stacks
usually has 5432 taken already.

**If the gateway will not start**, Windows has very likely reserved port 80 for
`http.sys` (check with `netsh interface ipv4 show excludedportrange protocol=tcp`).
Set `PORT_HTTP` to something free and add the same port to the three
`*_PUBLIC_URL` values, which are what ends up in links and token issuer claims.

## Renaming it

```powershell
./scripts/rename.ps1 -Name Acme        # add -WhatIf for a dry run
```

Rewrites namespaces, project names, folders, the solution, the compose project
and the database name. Run it once, before writing product code. If the database
name changed, drop the old volume with `docker compose down -v`.

## Layout

```
src/
  Saas.Auth.Api/    OpenIddict identity service
  Saas.Api/         resource API: tenancy, organizations, runs
  Saas.Business/    services and API contracts
  Saas.Core/        exceptions, logging, options, shared middleware
  Saas.Data/        EF Core, entities, migrations, RLS policies
  Saas.Api.Tests/   Reqnroll integration tests
  saas-ai/          FastAPI + LangGraph agent, and the arq worker
  saas-web/         Next.js + shadcn
gateway/traefik/    reverse proxy configuration
scripts/            certificate generation, rename
docs/               architecture and feature flags
```

## The platform operator

A SaaS has an owner, and the owner occasionally needs to look inside a
customer's workspace when that customer reports a problem. Set
`PLATFORM_ADMIN_EMAIL` in `.env` to the account that should hold that power:

```
PLATFORM_ADMIN_EMAIL=you@example.com
```

The identity service grants that account the `Administrator` role on startup.
If the account has not registered yet, the grant is applied on the next restart,
so the order does not matter.

What it unlocks:

- `GET /platform/organizations` — every workspace on the installation, with
  member and run counts. Read-only, and the only endpoint that sees across
  tenants.
- A **Platform** section in the sidebar listing those workspaces, each with an
  *Enter* button.

Entering a workspace re-mints the token against it, exactly like switching to
one you belong to. Two properties are deliberate:

- **The operator enters as a `Member`, never an `Owner`.** Support should not be
  able to delete a workspace or remove its last owner by accident.
- **Nothing bypasses row-level security.** Once inside, the operator is scoped
  to that one tenant like any other member. The cross-tenant listing is a
  `SECURITY DEFINER` function with a `SELECT`-only grant, not an exemption
  handed to the application role.

Entering a workspace is logged at warning level with both the operator and the
workspace, so looking at someone's data leaves a trail.

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — why the services are split
  the way they are, and how a request flows through them.
- [`docs/FEATURES.md`](docs/FEATURES.md) — every flag and what it changes.

## Testing

The integration tests start their own Postgres container with Testcontainers
(`pgvector/pgvector:pg17`, logins created by `src/Saas.Data/scripts/00-init.sh`)
and host `Saas.Api` and `Saas.Auth.Api` in-process from the working tree. The
database is real Postgres because the guarantee most worth testing is row-level
security, which an in-memory database cannot enforce. They need Docker, not the
compose stack.

```bash
dotnet test src/Saas.Api.Tests
```

## Deploying

Every service is an ordinary container, so the same images run on one VPS:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

Naming both files is what excludes `docker-compose.override.yml`, which is
development-only -- it publishes database ports and runs the services in
Development.

The production overlay adds a `websecure` entrypoint with automatic Lets Encrypt
certificates, redirects http to https, drops the Traefik dashboard, stops
publishing the database port, and scales Mailpit to zero so a real mail provider
has to be configured rather than silently swallowing every password reset.

Before the first deploy: point `*_HOST` and `*_PUBLIC_URL` at real domains, set
`ACME_EMAIL` and the `MAIL_*` variables, generate fresh signing certificates,
and set strong database passwords and a new `AUTH_SECRET`.
`ASPNETCORE_ENVIRONMENT=Production` makes the identity service refuse to start
without real signing certificates rather than falling back to ephemeral
development ones.

## Continuous integration

`.github/workflows/ci.yml` builds the solution, stands the stack up with Docker
Compose and runs the integration tests against it, then typechecks, lints and
builds the frontend and lints the AI service.
