FROM python:3.13-slim AS base

# uv resolves and installs far faster than pip and writes a lockfile, which is
# what makes the image reproducible.
COPY --from=ghcr.io/astral-sh/uv:latest /uv /usr/local/bin/uv

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    PATH="/srv/.venv/bin:$PATH"

WORKDIR /srv

# Dependencies first, so editing application code does not re-resolve them.
COPY pyproject.toml ./
RUN uv venv /srv/.venv && uv pip install --python /srv/.venv -r pyproject.toml

COPY app/ ./app/

# Run unprivileged.
RUN useradd --create-home --uid 10001 appuser && chown -R appuser /srv
USER appuser

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
