# Feature flags

Flags are set in `.env`, read by the .NET services, and surfaced to the frontend
by `GET /me` (and by the anonymous `GET /features` for the sign-up screen). The
UI never reads them from its own environment, so the backend stays the single
source of truth and the two cannot drift apart.

| Variable | Default | Effect |
|---|---|---|
| `FEATURE_ORGS` | `true` | Organization management: the workspace switcher, members, invitations. |
| `FEATURE_PUBLIC_SIGNUP` | `true` | Self-service registration. With it off, `/register` 404s and the endpoint refuses. |

One setting is not a feature flag but belongs next to them, because it is also
read from `.env` and also changes what the UI shows:

| Variable | Default | Effect |
|---|---|---|
| `PLATFORM_ADMIN_EMAIL` | empty | The account granted the platform `Administrator` role on startup. Empty means no operator account exists. |

See [ARCHITECTURE.md](ARCHITECTURE.md#the-platform-operator) for what the role
can and cannot do. In short: it can list every workspace and enter any of them
as a `Member`, and it does not bypass row-level security to do so.

## `FEATURE_ORGS`

**Organizations are always in the schema, and every user always has a personal
workspace, whatever this is set to.** The tenant is always an organization, so
there is exactly one code path. The flag only decides whether team features are
*exposed*.

With it off:

- The sidebar header shows the product logo instead of the workspace switcher —
  there is only one workspace, so a switcher would be a control that does
  nothing.
- Members and Invitations disappear from the sidebar, and navigating straight to
  `/settings/members` returns 404.
- `POST /organizations` and the invitation endpoints refuse with
  `OrganizationsDisabled`.
- Everything else — runs, the agent, the platform operator — works identically.

Turning it on later needs **no migration**: the tables, the memberships and the
`org_id` claim were there all along. That property is the whole reason the flag
works this way; the alternative (organizations absent from the schema when
disabled) would mean two divergent code paths and a painful data migration for
the first product that outgrows single-player.

Verify it round-trips:

```bash
sed -i 's/^FEATURE_ORGS=true$/FEATURE_ORGS=false/' .env
docker compose up -d api auth web
# switcher, members and invitations are gone; the app still works
sed -i 's/^FEATURE_ORGS=false$/FEATURE_ORGS=true/' .env
docker compose up -d api auth web
# they are back, against the same data
```

## Model and provider selection

| Variable | Default | Effect |
|---|---|---|
| `AI_DEFAULT_MODEL` | `gpt-6.1-sol` | The model, **and therefore the provider**. |
| `OPENAI_API_KEY` | — | Required when the model is an OpenAI one. |
| `ANTHROPIC_API_KEY` | — | Required when the model is an Anthropic one. |
| `AI_PROVIDER` | `openai` | Fallback only, for a model not yet in the registry. |

**Switching provider is one variable.** `src/saas-ai/app/llm.py` holds a registry
mapping each model to its provider and its prices, so naming a model selects
everything else:

```bash
AI_DEFAULT_MODEL=gpt-6.1-sol      # OpenAI    $2.00 / $10.00 per MTok
AI_DEFAULT_MODEL=claude-opus-5    # Anthropic $5.00 / $25.00 per MTok
```

Set the matching key and `docker compose up -d ai worker api`. Both SDKs are
installed; the client is imported lazily, so the unused one is never loaded.

`AI_DEFAULT_MODEL` goes to the API as well as the AI service, because the API
stamps the intended model onto each run row. Keep the two aligned, or the run
history records a model that was never actually called.

### Known models

| Provider | Models |
|---|---|
| OpenAI | `gpt-6-astra`, `gpt-6.1-sol`, `gpt-6-sol`, `gpt-6-luna`, `gpt-5.6-sol`, `gpt-5.6-luna`, `gpt-5.4-mini`, `gpt-5-mini`, `gpt-4o-mini` |
| Anthropic | `claude-opus-5`, `claude-sonnet-5`, `claude-haiku-4-5` |

A model missing from the registry still runs: it falls back to `AI_PROVIDER` and
that provider's most expensive listed prices, so an unrecognised model is
over-billed rather than silently billed at zero. Add it to `MODELS` with real
prices to fix the accounting.

The two providers report cached tokens differently — OpenAI folds them into the
input total, Anthropic reports them separately — so each entry carries a
`cache_included_in_input` flag. Getting it wrong double-counts or under-counts
the cheapest tokens in the bill.

## Google sign-in

| Variable | Default | Effect |
|---|---|---|
| `AUTH_GOOGLE_ID` | — | Blank hides the button entirely. |
| `AUTH_GOOGLE_SECRET` | — | Required alongside the id. |

One value configures both halves on purpose. The frontend uses the client id to
run the OAuth flow; the identity service uses the same id as the expected
audience when it verifies the resulting token. Feeding them separately is how a
provider ends up enabled in the UI and unverifiable on the server.

The exchange is a custom grant, `urn:saas:external`: the frontend hands Google's
ID token to the identity service, which validates it against Google's own
signing keys and then issues this application's tokens. The browser's claim
about who signed in is never trusted — only Google's signature over its own
token is.

An account is linked to an existing user only when the provider reports the
email address as verified, so signing up with an unverified address cannot take
over somebody else's account.

## Gateway port

| Variable | Default |
|---|---|
| `PORT_HTTP` | `80` |

Windows frequently reserves port 80 for `http.sys`, which stops Docker binding
it. Set this to a free port and add the same port to `WEB_PUBLIC_URL`,
`API_PUBLIC_URL` and `AUTH_PUBLIC_URL` -- those are separate from the `*_HOST`
values because they are what goes into emailed links and the token issuer
claim, and they stop matching the host names the moment the port is not 80.

## Things that are deliberately not flags

- **Billing and usage metering.** Neither exists. The template deliberately
  stops at the point where products diverge most: what you meter, how you price
  it and which provider you bill through are product decisions, and a guessed
  schema is worse than none. `RunEntity` is the hook — one row per agent run,
  already tenant-scoped — if you want to start counting.
- **Row-level security.** Always on. It is the layer that turns a missed filter
  into zero rows instead of a breach, and a flag to disable it would be a flag to
  disable tenant isolation.
- **Email.** Mailpit catches everything locally and shows it at
  <http://localhost:8025>, so password resets and invitations are fully
  exercisable without a provider account. Point `Mail__*` at a real provider for
  production -- the production overlay scales Mailpit to zero precisely so that
  forgetting is loud rather than silent.
- **Rate limiting.** Always on. Per client IP: 60/min on the identity service,
  5/min on registration and password-reset requests, 300/min on the resource
  API. Account lockout alone does not stop one password being tried against
  thousands of accounts.
