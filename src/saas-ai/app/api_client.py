"""HTTP client for the .NET API.

Every request forwards the caller's own access token. That is the whole
authorization model for this service: it never holds credentials, so anything it
can read, the user could have read themselves -- and anything it cannot, the
database refuses by returning no rows.
"""

from typing import Any

import httpx

from app.auth import CallerIdentity
from app.settings import Settings


class ApiError(RuntimeError):
    """A non-success response from the .NET API."""

    def __init__(self, status_code: int, detail: str) -> None:
        super().__init__(f"API returned {status_code}: {detail}")
        self.status_code = status_code
        self.detail = detail


class ApiClient:
    def __init__(self, settings: Settings, caller: CallerIdentity) -> None:
        self._caller = caller
        self._client = httpx.AsyncClient(
            base_url=settings.api_base_url,
            timeout=settings.api_timeout_seconds,
            headers={"Authorization": f"Bearer {caller.access_token}"},
        )

    async def __aenter__(self) -> "ApiClient":
        return self

    async def __aexit__(self, *_: object) -> None:
        await self.aclose()

    async def aclose(self) -> None:
        await self._client.aclose()

    async def get_run(self, run_id: str) -> dict[str, Any]:
        """Read the run the API authorised.

        A run belonging to another tenant comes back as 404, because the query
        filter and the row-level security policy both scope it to the
        organization in the caller's token. This call *is* the authorization
        check -- there is no separate handoff token to verify.
        """
        return await self._get(f"/internal/runs/{run_id}")

    async def update_run_status(
        self,
        run_id: str,
        status: str,
        error: str | None = None,
    ) -> None:
        await self._request(
            "PUT",
            f"/internal/runs/{run_id}/status",
            json={"status": status, "error": error},
        )

    async def list_runs(self, thread_id: str) -> list[dict[str, Any]]:
        """Runs on a conversation thread, scoped to the caller tenant.

        Used to authorise reading a thread history: row-level security means an
        empty list is indistinguishable from a thread that belongs to someone
        else, which is exactly the answer wanted.
        """
        response = await self._request("GET", f"/runs?threadId={thread_id}")
        return response.json()

    async def get_me(self) -> dict[str, Any]:
        """Backs the example agent tool."""
        return await self._get("/me")

    async def _get(self, path: str) -> dict[str, Any]:
        response = await self._request("GET", path)
        return response.json()

    async def _request(self, method: str, path: str, **kwargs: Any) -> httpx.Response:
        response = await self._client.request(method, path, **kwargs)

        if response.is_error:
            raise ApiError(response.status_code, response.text[:500])

        return response
