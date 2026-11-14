"""The AI service.

Owns the data plane only: the stream of tokens from the model to the browser.
Authorization and every other business decision stay in the .NET API, which is
why the first thing this service does with a run is ask the API for it.
"""

import json
import logging
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Any

from fastapi import Depends, FastAPI, HTTPException, status
from sse_starlette.sse import EventSourceResponse

from app.api_client import ApiClient, ApiError
from app.auth import CallerIdentity, authenticate
from app.graph import (
    StreamCompleted,
    StreamedToken,
    StreamedToolCall,
    checkpointer,
    load_thread,
    run_agent,
)
from app.settings import Settings, get_settings

logger = logging.getLogger("saas.ai")

# Permission names mirror Saas.Data.Models.Constants.Permissions.
PERMISSION_RUN_READ = "run.read"


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    """Open the checkpointer once for the process, not once per request.

    Each AsyncPostgresSaver owns a connection pool; building one per request
    would open a pool per streamed message.
    """
    settings = get_settings()

    async with checkpointer(settings) as saver:
        # Creates the checkpointer's tables in the `ai` schema on first boot.
        await saver.setup()
        app.state.saver = saver
        logger.info("Checkpointer ready; agent service started.")
        yield


app = FastAPI(
    title="Saas AI",
    description="Streaming LangGraph agent service.",
    lifespan=lifespan,
)


@app.get("/health", include_in_schema=False)
async def health() -> dict[str, str]:
    return {"status": "healthy"}


@app.get("/threads/{thread_id}/messages")
async def thread_messages(
    thread_id: str,
    caller: CallerIdentity = Depends(authenticate),
    settings: Settings = Depends(get_settings),
) -> dict[str, Any]:
    """Replay a conversation, so a reload does not lose the transcript.

    Authorised the same way as everything else here: the .NET API is asked for
    the runs on this thread with the caller own token, and row-level security
    decides whether any come back. A thread belonging to another tenant yields
    an empty list and therefore a 404 -- the database makes the decision, not
    this endpoint.
    """
    caller.require(PERMISSION_RUN_READ)

    async with ApiClient(settings, caller) as api:
        try:
            runs = await api.list_runs(thread_id)
        except ApiError as error:
            raise HTTPException(
                status_code=status.HTTP_502_BAD_GATEWAY,
                detail="Could not verify the thread.",
            ) from error

    if not runs:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Thread not found.")

    messages = await load_thread(saver=app.state.saver, thread_id=thread_id)

    return {"threadId": thread_id, "messages": messages}


@app.get("/runs/{run_id}/stream")
async def stream_run(
    run_id: str,
    caller: CallerIdentity = Depends(authenticate),
    settings: Settings = Depends(get_settings),
) -> EventSourceResponse:
    """Execute a run and stream its output as server-sent events.

    The run must already exist: the .NET API created it after authorizing the
    caller. This endpoint re-reads it through
    `GET /internal/runs/{id}` using the caller's own token, so a run belonging
    to another organization comes back as 404 from the database's row-level
    security rather than from a check written here.
    """
    caller.require(PERMISSION_RUN_READ)

    api = ApiClient(settings, caller)

    try:
        run = await api.get_run(run_id)
    except ApiError as error:
        await api.aclose()
        if error.status_code in (status.HTTP_403_FORBIDDEN, status.HTTP_404_NOT_FOUND):
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Run not found.",
            ) from error
        raise HTTPException(
            status_code=status.HTTP_502_BAD_GATEWAY,
            detail="Could not load the run.",
        ) from error

    if run.get("status") not in ("Pending", 0):
        await api.aclose()
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="This run has already been started.",
        )

    return EventSourceResponse(
        _event_stream(api=api, settings=settings, run=run),
        # Nginx-style proxies buffer by default, which would hold tokens back
        # until the response ends and silently turn streaming into one blob.
        headers={"X-Accel-Buffering": "no", "Cache-Control": "no-cache"},
    )


async def _event_stream(
    *,
    api: ApiClient,
    settings: Settings,
    run: dict[str, Any],
) -> AsyncIterator[dict[str, str]]:
    """Drive one run, reporting its final status back to the .NET API."""
    run_id = run["id"]
    model = run.get("model") or settings.ai_default_model

    async with api:
        try:
            await api.update_run_status(run_id, "Running")

            async for event in run_agent(
                settings=settings,
                api=api,
                saver=app.state.saver,
                thread_id=run["threadId"],
                user_input=run["input"],
                model=model,
            ):
                match event:
                    case StreamedToken(text=text):
                        yield {"event": "token", "data": json.dumps({"text": text})}

                    case StreamedToolCall(name=name):
                        yield {"event": "tool", "data": json.dumps({"name": name})}

                    case StreamCompleted():
                        await api.update_run_status(run_id, "Completed")

                        yield {"event": "done", "data": json.dumps({"runId": run_id})}

        except Exception as error:  # noqa: BLE001 - the stream must always close cleanly
            logger.exception("Run %s failed", run_id)

            # Best effort: if the API is what failed, this will fail too, and the
            # run is left Running for a reaper to time out.
            try:
                await api.update_run_status(run_id, "Failed", str(error)[:500])
            except ApiError:
                logger.warning("Could not mark run %s as failed", run_id)

            # The browser has already received a 200 and an open stream, so an
            # HTTP error code is no longer available -- report it in-band.
            yield {
                "event": "error",
                "data": json.dumps({"message": "The assistant could not finish this run."}),
            }
