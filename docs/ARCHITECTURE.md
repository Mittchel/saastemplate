# Architecture

Five services, each with one job, behind a reverse proxy.

```
                         ┌───────────┐
      browser ──────────▶│  Traefik  │  :80, routes by hostname
                         └─────┬─────┘
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
   ┌────────────┐      ┌──────────────┐      ┌────────────┐
   │    web     │      │   identity   │      │    api     │
   │  Next.js   │      │ .NET +       │      │ .NET       │
   │  shadcn    │      │ OpenIddict   │      │ controllers│
   └─────┬──────┘      └──────┬───────┘      └─────┬──────┘
         │ SSE proxy          │ JWKS               │
         ▼                    └───────┬────────────┘
   ┌────────────┐                     │ same key, same claims
   │     ai     │◀────────────────────┘
   │  FastAPI   │──── tools, as the user ────▶ api
   │  LangGraph │
   └─────┬──────┘        worker (same image, arq)
         │                     │
         └──────────┬──────────┘
                    ▼
       Postgres (+ pgvector)        Redis
       public schema │ ai schema
```

## Why the services are split this way

**Python owns the agent.** LangGraph's Python package is the reference
implementation; the JS port trails it, and the surrounding toolchain — document
parsing, evaluation, embeddings — is Python too. The cost is a second language,
which is contained by keeping the service deliberately small: it owns graphs,
prompts, tools and model calls. No business logic, no authorization decisions,
no user management.

**The .NET API is not in the token stream.** It makes every decision that
matters — who the caller is, what they may do, recording the run — and then
gets out of the way. Relaying tokens through ASP.NET would add a hop
that transforms nothing, hold a connection open for the length of a run, and
risk the response-buffering middleware turning a stream into one blob.

**Next.js *is* in the token stream**, for one reason: the access token lives in
an httpOnly cookie, so browser JavaScript cannot set an `Authorization` header.
`/api/chat/[runId]/stream` is the browser's own origin attaching the bearer
token to a pass-through. That hop carries a credential; the .NET one would not.

**Traefik, not YARP.** YARP would be another .NET application to build, deploy
and patch for something Traefik does in twenty lines of labels, with automatic
Let's Encrypt on top.

## A chat turn, end to end

```
1. browser ──POST /runs──▶ api
      authorizes (membership row lookup), inserts a Run row,
      returns { id, threadId }.

2. browser ──GET /api/chat/{id}/stream──▶ web
      attaches the access token from the cookie, proxies to ai.

3. ai ──GET /internal/runs/{id}──▶ api        (carrying the user's token)
      row-level security decides whether the run is visible.
      A run from another tenant is a 404 from the database, not a check in code.

4. ai runs the graph, streaming tokens back as SSE.
      A tool call re-enters api with the same token, so the agent is authorized
      exactly like a UI click.

5. ai ──PUT /internal/runs/{id}/status──▶ api     Succeeded or Failed
```

Note what is absent: no service account, no shared secret, no bespoke handoff
token. The AI service only ever acts as the user whose token it was handed.

## Tenancy

The active organization travels in the access token as `org_id`. Both the .NET
API and the Python service read it from the same claim in the same token, so
neither needs a database lookup to establish the tenant — and neither can be
talked into changing it, because switching means asking the identity service for
a new token, which re-checks membership first.

Isolation is enforced twice:

1. **EF Core global query filters** (`ApplicationDbContext`) — layer one. Makes
   ordinary application code behave correctly and fail with good errors.
2. **Postgres row-level security** (the `RowLevelSecurity` migration) — layer
   two. Makes a *missed* filter return zero rows instead of another tenant's
   data.

Layer two only works because the runtime connects as `saas_app`, which does not
own the tables, and because the policies are `FORCE`d so they apply even if a
deployment gets the roles wrong.

### Three database roles

| Role | Used by | Can do |
|---|---|---|
| `saas_owner` | migrations | owns the tables; creates and alters them |
| `saas_app` | api, identity | read/write under RLS; cannot alter schema |
| `saas_ai` | ai, worker | owns the `ai` schema only; **no** access to business tables |

The third one is the point: a compromised agent cannot query around the API,
because its credentials reach nothing but LangGraph checkpoints.

### Bootstrap writes

Two operations legitimately write outside the caller's active tenant: signing up
(create user + personal organization + owner membership) and creating an extra
organization. Both insert a membership row for an organization that is not yet
the active one, which the policy correctly rejects.

Rather than loosening the policy on `Memberships` — the one table where a forged
row grants cross-tenant access — those paths widen the tenant for a single
transaction via `TenantSession.SetLocalOrganizationAsync`. The policy stays
strict everywhere else.

### A consequence worth knowing

Row-level security applies to *reads the application makes about other tenants
too*, including uniqueness checks. Organization slugs are globally unique, but
`saas_app` cannot see another tenant's slug, so a check-then-insert would report
a free name and then fail. `ProvisioningService` therefore lets the unique index
be the authority and retries on violation. Expect to meet this pattern again any
time a global constraint crosses a tenant boundary.

## Authentication

- **Grants:** password and refresh only. The Next.js app renders the login form
  (`login-03`) and exchanges credentials, which is what keeps the sign-in screen
  under the frontend's design control.
- **Switching organization** is the refresh grant plus an `organization_id`
  parameter. The client already holds a refresh token and already calls that
  endpoint; membership validation is the only extra step, and token rotation
  comes free. A custom grant would have added a concept without adding a
  guarantee.
- **`DisableAccessTokenEncryption` is mandatory**, not stylistic: the Python
  service validates these as ordinary JWTs from the JWKS endpoint, which is
  impossible if they arrive encrypted.
- **The issuer is pinned** (`OpenIddict:Issuer`). OpenIddict otherwise derives
  `iss` from the request host, so the same service would hand out tokens
  claiming `http://auth.localhost/` through the gateway and
  `http://localhost:5001/` on its published port — and a consumer validating the
  issuer would reject whichever one it was not told about.
- **Signing certificates are real files on a volume.** The development
  certificates regenerate on every boot, which invalidates every token the API
  and the AI service have already been told to trust.
- **Roles expand into permissions at token time.** The identity service turns an
  organization role into `permission` claims, so the API only ever checks
  permissions and never has to know about roles.
- **Social sign-in is a third grant**, `urn:saas:external`, not a side-door
  endpoint — so those tokens get the same claims, lifetimes and rotation as
  every other sign-in. The identity service validates the provider's ID token
  against the provider's own JWKS; the frontend is a courier, not an authority.
  An existing account is only linked when the provider says the address is
  verified, because otherwise signing up with an unverified address would be
  account takeover.

## Operational concerns

**Rate limiting** is partitioned by client IP, which requires
`UseForwardedProxyHeaders` to run before the limiter — without it every request
appears to come from Traefik, the whole deployment shares one bucket, and one
abusive client throttles everybody. Account lockout is not a substitute: it
stops one password being ground against one account, but not one password being
tried against thousands of accounts, and it hands anyone a way to lock a victim
out deliberately.

**Stale runs** are swept by a background service in the API. A run is marked
`Running` before the model is called; if the AI service dies — or the call
reporting the outcome is itself what failed — the row would stay `Running`
forever and the conversation would never resolve.

That sweep is the one place that genuinely needs to see across tenants, which is
exactly what row-level security exists to prevent. Rather than weakening the
policies or giving a background job a role that bypasses them, it is a
`SECURITY DEFINER` function owned by `saas_maintenance`: a role with
`BYPASSRLS`, **no `LOGIN`**, and a grant on nothing but `SELECT, UPDATE` on
`Runs`. The exemption is bounded by the body of one audited function that can
only move timed-out rows to `Failed`.

Note that `BYPASSRLS` exempts a role from the policies, not from ordinary table
privileges — both are needed, and forgetting the second produces a sweep that
silently finds nothing.

## The platform operator

Someone has to run the SaaS, and that person needs to see workspaces they are
not a member of. This is the one place where the tenancy model is deliberately
crossed, so it is worth being precise about how far the crossing goes.

The operator is an account holding the `Administrator` role, granted on startup
to `Platform:AdministratorEmail`. When the identity service mints a token for
them it adds `is_platform_admin`. Two things read that claim, and nothing else
does:

1. **`GET /platform/organizations`** — the cross-tenant listing, behind
   `[Authorize(Policy = platform.administrator)]`.
2. **The organization switch** — the usual path requires a membership row; an
   operator may instead name any workspace that exists.

### What is not crossed

The listing does **not** work by exempting the application role from row-level
security. `saas_app` stays subject to `FORCE ROW LEVEL SECURITY` exactly as it
is for every other request. Instead the listing is a `SECURITY DEFINER`
function, `app_list_organizations()`, owned by `saas_maintenance` — the same
no-login, bounded-grant role the run reaper uses — with `SELECT` on
`Organizations` and `Memberships` and nothing more. The widest thing an operator
can do is read the names and counts that one function returns.

Entering a workspace grants `OrganizationRole.Member`, not `Owner`: support
work does not require the ability to delete a workspace or remove its last
owner. From that point the operator *is* an ordinary member as far as the
database is concerned — one `org_id` in the token, the same policies, the same
query filters. They see one tenant, not all of them.

There is no membership row backing that session, which `/me` has to account
for: it synthesises a `Member` membership for an operator inside a foreign
workspace rather than refusing the request, and appends it to the switcher list
so they have a way back out.

The switch is logged at warning level with the operator and the target, because
reading a customer's data should leave a record even when it is legitimate.
