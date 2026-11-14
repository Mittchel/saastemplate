# CLAUDE.md

## Testing

- Integration tests live in `src/Saas.Api.Tests` (Reqnroll, run with `dotnet test src/Saas.Api.Tests`).
- They use Testcontainers and in-process hosts: one `pgvector/pgvector:pg17` Postgres container per run, with the
  `saas_owner` / `saas_app` / `saas_ai` logins created by running `src/Saas.Data/scripts/00-init.sh` in it, and
  `Saas.Api` and `Saas.Auth.Api` hosted with `WebApplicationFactory` from the working tree (see `Drivers/TestHosts.cs`).
  The container is removed when the run ends. The AI service is not hosted.
- The test hosts generate their own signing and encryption certificates (`Drivers/TestCertificates.cs`) as `.pfx` files
  in a temporary folder that is removed when the run ends, so the tests need no certificate store or keychain.
- Running the tests needs Docker, not the compose stack. Do not start `docker compose` for them, and do not point
  them at `localhost:5001` / `localhost:5002`.
- Postgres stays real: row-level security is what these tests exist for, so never swap in an in-memory or SQLite database.
