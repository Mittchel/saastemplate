"""Background worker.

Shares the service image and differs only in entrypoint. It exists so the first
job a product needs -- document ingestion, embedding, scheduled summaries --
drops in without introducing a new deployable, a new image or a new connection
story.

Queue a job from the API service or from a request handler with:

    from arq import create_pool
    from arq.connections import RedisSettings

    pool = await create_pool(RedisSettings.from_dsn(settings.redis_url))
    await pool.enqueue_job("example_job", "some-argument")
"""

import logging

from arq.connections import RedisSettings

from app.settings import get_settings

logger = logging.getLogger("saas.ai.worker")


async def example_job(ctx: dict, subject: str) -> str:
    """A placeholder job, kept because arq refuses to start with none.

    Replace it with the first real job rather than adding alongside it. The
    signature is the pattern to copy: `ctx` carries the worker context (Redis
    pool, job id, retry count) and the remaining arguments are whatever was
    enqueued.
    """
    logger.info("example_job ran for %s (job %s)", subject, ctx.get("job_id"))

    return f"processed {subject}"


async def startup(ctx: dict) -> None:
    logger.info("Worker started.")


async def shutdown(ctx: dict) -> None:
    logger.info("Worker stopped.")


class WorkerSettings:
    """arq entrypoint: `arq app.worker.WorkerSettings`."""

    functions = [example_job]
    on_startup = startup
    on_shutdown = shutdown

    # Keep retries bounded; a job that fails five times needs a human, not
    # another attempt.
    max_tries = 5
    job_timeout = 300

    # A plain attribute, not a method: arq reads this off the class and expects
    # a RedisSettings instance, so a staticmethod is handed through unevaluated.
    redis_settings = RedisSettings.from_dsn(get_settings().redis_url)
