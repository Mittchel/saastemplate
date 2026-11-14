"""Access-token validation.

The same tokens the .NET API validates, checked against the same JWKS endpoint.
There is no shared secret between the services and no service account: this
service only ever acts as the user whose token it was handed, which is what lets
row-level security decide what it can see.
"""

from dataclasses import dataclass

import jwt
from fastapi import Depends, HTTPException, Request, status
from jwt import PyJWKClient

from app.settings import Settings, get_settings

# Caches the signing keys and refetches on rotation, so the JWKS endpoint is not
# hit per request.
_jwk_client: PyJWKClient | None = None


def _get_jwk_client(settings: Settings) -> PyJWKClient:
    global _jwk_client
    if _jwk_client is None:
        _jwk_client = PyJWKClient(settings.jwks_url, cache_keys=True, lifespan=600)
    return _jwk_client


@dataclass(frozen=True, slots=True)
class CallerIdentity:
    """The authenticated user and the tenant their token is scoped to."""

    user_id: str
    organization_id: str
    email: str
    permissions: frozenset[str]

    # The raw token, forwarded verbatim on every call back into the .NET API.
    # Forwarding rather than re-minting is the point: the API applies exactly
    # the same authorization to an agent's tool call as to a UI click.
    access_token: str

    def require(self, permission: str) -> None:
        if permission not in self.permissions:
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail=f"Missing permission: {permission}",
            )


def _bearer_token(request: Request) -> str:
    header = request.headers.get("Authorization", "")

    scheme, _, token = header.partition(" ")
    if scheme.lower() != "bearer" or not token:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="A bearer access token is required.",
            headers={"WWW-Authenticate": "Bearer"},
        )

    return token


def authenticate(
    request: Request,
    settings: Settings = Depends(get_settings),
) -> CallerIdentity:
    token = _bearer_token(request)

    try:
        signing_key = _get_jwk_client(settings).get_signing_key_from_jwt(token)
        claims = jwt.decode(
            token,
            signing_key.key,
            algorithms=["RS256"],
            # The access token is not audience-restricted; OpenIddict scopes it
            # by resource instead, and authorization is enforced by the
            # permission claims below and by RLS in the database.
            options={"verify_aud": False},
        )
    except jwt.PyJWTError as error:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail=f"Invalid access token: {error}",
            headers={"WWW-Authenticate": "Bearer"},
        ) from error

    if claims.get("iss") not in settings.valid_issuers:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Token issued by an unexpected authority.",
        )

    user_id = claims.get("sub")
    organization_id = claims.get("org_id")

    # No tenant means nothing this service does can be scoped, so refuse rather
    # than fall back to something broader.
    if not user_id or not organization_id:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="The token carries no user or organization.",
        )

    raw_permissions = claims.get("permission", [])
    if isinstance(raw_permissions, str):
        raw_permissions = [raw_permissions]

    return CallerIdentity(
        user_id=user_id,
        organization_id=organization_id,
        email=claims.get("email", ""),
        permissions=frozenset(raw_permissions),
        access_token=token,
    )
