"""Configuration, read from the environment that docker compose provides."""

from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # --- Identity ----------------------------------------------------------
    # Reachable on the container network; used to fetch JWKS.
    auth_issuer: str = "http://auth:8080"
    # The issuer as it appears in the token's `iss` claim: the public hostname
    # tokens were minted behind. Differs from auth_issuer for the same reason
    # the .NET API keeps both.
    auth_public_issuer: str = "http://auth.localhost"

    # --- The .NET API ------------------------------------------------------
    # Every call to it carries the end user's own access token; this service
    # holds no credentials of its own and no service account.
    api_base_url: str = "http://api:8080"
    api_timeout_seconds: float = 30.0

    # --- Storage -----------------------------------------------------------
    # Its own role and its own schema. Deliberately has no access to the
    # business tables: business data is reached only through the API.
    ai_database_url: str = ""
    redis_url: str = "redis://redis:6379/0"

    # --- Model -------------------------------------------------------------
    # The provider is normally inferred from ai_default_model via the registry
    # in llm.py. This only decides the fallback for a model that is not listed
    # there yet, so a brand-new release still runs.
    ai_provider: str = "openai"

    openai_api_key: str = ""
    anthropic_api_key: str = ""

    # Switching provider is this one variable; see llm.py.
    ai_default_model: str = "gpt-6.1-sol"
    ai_max_tokens: int = 4096
    ai_recursion_limit: int = 12

    log_level: str = "info"

    @property
    def valid_issuers(self) -> set[str]:
        """Each configured issuer with and without a trailing slash.

        OpenIddict issues `iss` with a trailing slash while the configured URLs
        are written without one, and the comparison is exact -- the .NET API
        needs the same normalisation.
        """
        issuers: set[str] = set()
        for issuer in (self.auth_public_issuer, self.auth_issuer):
            if issuer:
                issuers.add(issuer.rstrip("/"))
                issuers.add(issuer.rstrip("/") + "/")
        return issuers

    @property
    def jwks_url(self) -> str:
        return f"{self.auth_issuer.rstrip('/')}/.well-known/jwks"

    @property
    def psycopg_connection_string(self) -> str:
        """psycopg wants a libpq URI; accept the postgresql:// form as given."""
        return self.ai_database_url


@lru_cache
def get_settings() -> Settings:
    return Settings()
