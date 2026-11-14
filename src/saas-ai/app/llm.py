"""Model access.

The single place a model is constructed, so swapping providers is a config
change rather than an edit spread across the graph.

**Switching provider is one environment variable.** `AI_DEFAULT_MODEL` names a
model; the provider is looked up from the registry below, so

    AI_DEFAULT_MODEL=gpt-6.1-sol      -> OpenAI
    AI_DEFAULT_MODEL=claude-opus-5    -> Anthropic

and nothing else changes. Set the matching API key and restart the service.
"""

from enum import StrEnum

from langchain_core.language_models.chat_models import BaseChatModel

from app.settings import Settings


class Provider(StrEnum):
    OPENAI = "openai"
    ANTHROPIC = "anthropic"


# Which provider owns which model. Adding a model is one line; a model that is
# not listed still runs, falling back to AI_PROVIDER.
MODEL_PROVIDERS: dict[str, Provider] = {
    # --- OpenAI --------------------------------------------------------------
    "gpt-6-astra": Provider.OPENAI,
    "gpt-6.1-sol": Provider.OPENAI,
    "gpt-6-sol": Provider.OPENAI,
    "gpt-6-luna": Provider.OPENAI,
    "gpt-5.6-sol": Provider.OPENAI,
    "gpt-5.6-luna": Provider.OPENAI,
    "gpt-5.4-mini": Provider.OPENAI,
    "gpt-5-mini": Provider.OPENAI,
    "gpt-4o-mini": Provider.OPENAI,
    # --- Anthropic -----------------------------------------------------------
    "claude-opus-5": Provider.ANTHROPIC,
    "claude-sonnet-5": Provider.ANTHROPIC,
    "claude-haiku-4-5": Provider.ANTHROPIC,
}


def resolve_provider(model: str, settings: Settings) -> Provider:
    """Find which provider owns a model.

    An unknown model is not an error: a provider can release one faster than
    this table is updated, so it falls back to the configured provider.
    """
    return MODEL_PROVIDERS.get(model, Provider(settings.ai_provider))


def build_model(settings: Settings, model: str | None = None) -> BaseChatModel:
    """Construct the chat model for whichever provider owns it.

    Imports are deferred so the service starts with only the provider actually
    in use installed -- a deployment that never touches Anthropic does not need
    its SDK importable.
    """
    name = model or settings.ai_default_model
    provider = resolve_provider(name, settings)

    if provider is Provider.OPENAI:
        from langchain_openai import ChatOpenAI

        if not settings.openai_api_key:
            raise RuntimeError(f"Model {name} is an OpenAI model but OPENAI_API_KEY is not set.")

        return ChatOpenAI(
            model=name,
            api_key=settings.openai_api_key,
            max_tokens=settings.ai_max_tokens,
            streaming=True,
        )

    from langchain_anthropic import ChatAnthropic

    if not settings.anthropic_api_key:
        raise RuntimeError(f"Model {name} is an Anthropic model but ANTHROPIC_API_KEY is not set.")

    # Thinking is left unconfigured on purpose: it is on by default for the
    # current Claude models, and passing a fixed budget is rejected by them.
    return ChatAnthropic(
        model=name,
        api_key=settings.anthropic_api_key,
        max_tokens=settings.ai_max_tokens,
        streaming=True,
    )
