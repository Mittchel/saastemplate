"""The agent graph and its conversation state.

A ReAct agent: the model decides whether to answer or call a tool, tool results
come back, and it loops until it answers. Deliberately the simplest thing that
exercises every pattern a product will reuse -- streaming, tool calls that
re-enter the .NET API as the user, and durable conversation state. Replace the
graph; keep the plumbing.
"""

from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from dataclasses import dataclass

from langchain_core.messages import AIMessageChunk, HumanMessage, SystemMessage
from langgraph.checkpoint.postgres.aio import AsyncPostgresSaver
from langgraph.prebuilt import create_react_agent

from app.api_client import ApiClient
from app.llm import build_model
from app.settings import Settings
from app.tools import build_tools

SYSTEM_PROMPT = """You are the assistant embedded in a SaaS application.

Answer in plain, direct prose. Use the tools available to you when the user asks \
about their own account, workspace or permissions rather than guessing or \
asking them to repeat information the application already knows.

If a tool fails, say what you could not find out instead of inventing an answer."""


@dataclass(slots=True)
class StreamedToken:
    """One chunk of assistant text."""

    text: str


@dataclass(slots=True)
class StreamedToolCall:
    """A tool the agent decided to call, surfaced so the UI can show progress."""

    name: str


@dataclass(slots=True)
class StreamCompleted:
    """End of the run."""

    model: str


StreamEvent = StreamedToken | StreamedToolCall | StreamCompleted


@asynccontextmanager
async def checkpointer(settings: Settings) -> AsyncIterator[AsyncPostgresSaver]:
    """Open the conversation store.

    Writes to the `ai` schema under this service's own database role, which has
    no access to the business tables. Conversation state and business data are
    deliberately not reachable from the same credentials.
    """
    async with AsyncPostgresSaver.from_conn_string(settings.psycopg_connection_string) as saver:
        yield saver


async def run_agent(
    *,
    settings: Settings,
    api: ApiClient,
    saver: AsyncPostgresSaver,
    thread_id: str,
    user_input: str,
    model: str | None = None,
) -> AsyncIterator[StreamEvent]:
    """Execute one turn, yielding events as they happen.

    The thread id is what makes a conversation durable: the checkpointer loads
    prior messages for that thread, so only the new user turn is passed in here
    rather than the whole history.
    """
    chat_model = build_model(settings, model)

    agent = create_react_agent(
        model=chat_model,
        tools=build_tools(api),
        checkpointer=saver,
    )

    config = {
        "configurable": {"thread_id": thread_id},
        # A tool-calling loop can in principle never terminate; this bounds it.
        "recursion_limit": settings.ai_recursion_limit,
    }

    resolved_model = model or settings.ai_default_model

    # "messages" streams per-token; prior turns come from the checkpointer, so
    # the system prompt is sent each turn and the history is not.
    async for chunk, _metadata in agent.astream(
        {"messages": [SystemMessage(SYSTEM_PROMPT), HumanMessage(user_input)]},
        config=config,
        stream_mode="messages",
    ):
        if not isinstance(chunk, AIMessageChunk):
            continue

        if chunk.content:
            for block in _text_blocks(chunk.content):
                yield StreamedToken(block)

        for tool_call in chunk.tool_call_chunks or []:
            if tool_call.get("name"):
                yield StreamedToolCall(tool_call["name"])

    yield StreamCompleted(model=resolved_model)


def _text_blocks(content: str | list) -> list[str]:
    """Normalise LangChain content, which is a string or a list of blocks."""
    if isinstance(content, str):
        return [content] if content else []

    blocks: list[str] = []
    for block in content:
        if isinstance(block, str):
            blocks.append(block)
        elif isinstance(block, dict) and block.get("type") == "text":
            text = block.get("text", "")
            if text:
                blocks.append(text)

    return blocks


async def load_thread(
    *,
    saver: AsyncPostgresSaver,
    thread_id: str,
) -> list[dict[str, str]]:
    """Read a conversation back out of the checkpointer.

    The assistant turns exist only here -- the .NET side records that a run
    happened and what it cost, not what was said -- so restoring a transcript
    means reading the graph state rather than the business database.
    """
    config = {"configurable": {"thread_id": thread_id}}

    checkpoint = await saver.aget(config)
    if not checkpoint:
        return []

    messages = checkpoint.get("channel_values", {}).get("messages", [])

    transcript: list[dict[str, str]] = []
    for message in messages:
        role = getattr(message, "type", None)

        # System prompts are resent on every turn and are not part of the
        # conversation the user had; tool traffic is noise in a transcript.
        if role not in ("human", "ai"):
            continue

        text = "".join(_text_blocks(getattr(message, "content", "")))
        if not text.strip():
            continue

        transcript.append({"role": "user" if role == "human" else "assistant", "text": text})

    return transcript
