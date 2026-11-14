/**
 * Direct calls to the identity service's token endpoint.
 *
 * Kept out of the Auth.js config so the three exchanges the app performs --
 * sign in, renew, switch organization -- read as the same shape, because they
 * are: all three are a POST to /connect/token that returns a new token pair.
 */

import { OIDC_SCOPES, authIssuerInternal, oidcClientId, oidcClientSecret } from "@/lib/config";

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
  /** Epoch milliseconds. */
  expiresAt: number;
  organizationId: string;
  organizationRole: string | null;
}

interface TokenEndpointResponse {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
  error?: string;
  error_description?: string;
}

export class TokenError extends Error {
  constructor(
    message: string,
    readonly code: string,
  ) {
    super(message);
  }
}

/** Reads the tenant claims straight off the access token. */
function readClaims(accessToken: string): { organizationId: string; organizationRole: string | null } {
  const payload = accessToken.split(".")[1];

  if (!payload) {
    return { organizationId: "", organizationRole: null };
  }

  // Signature verification is the API's job, not the client's; this only reads
  // claims the server already vouched for by issuing the token.
  const decoded = JSON.parse(Buffer.from(payload, "base64url").toString("utf8"));

  return {
    organizationId: decoded.org_id ?? "",
    organizationRole: decoded.org_role ?? null,
  };
}

async function exchange(body: Record<string, string>): Promise<TokenPair> {
  const response = await fetch(`${authIssuerInternal()}/connect/token`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      client_id: oidcClientId(),
      client_secret: oidcClientSecret(),
      ...body,
    }),
    cache: "no-store",
  });

  const payload = (await response.json()) as TokenEndpointResponse;

  if (!response.ok || payload.error) {
    throw new TokenError(
      payload.error_description ?? "The identity service rejected the request.",
      payload.error ?? "invalid_grant",
    );
  }

  return {
    accessToken: payload.access_token,
    // Refresh tokens rotate, so a response without one would silently end the
    // session on the next renewal.
    refreshToken: payload.refresh_token ?? "",
    expiresAt: Date.now() + payload.expires_in * 1000,
    ...readClaims(payload.access_token),
  };
}

/** Exchanges the credentials collected by the login form. */
export function signInWithPassword(email: string, password: string): Promise<TokenPair> {
  return exchange({
    grant_type: "password",
    username: email,
    password,
    scope: OIDC_SCOPES,
  });
}

/** Renews an expiring access token. */
export function renew(refreshToken: string): Promise<TokenPair> {
  return exchange({ grant_type: "refresh_token", refresh_token: refreshToken });
}

/**
 * Exchanges a social provider ID token for this application tokens.
 *
 * The provider token is verified by the identity service against the provider
 * own signing keys, so nothing here is trusted: this call is a courier, not an
 * authority.
 */
export function signInWithExternal(provider: string, idToken: string): Promise<TokenPair> {
  return exchange({
    grant_type: "urn:saas:external",
    provider,
    id_token: idToken,
    scope: OIDC_SCOPES,
  });
}

/**
 * Switches the active organization.
 *
 * Deliberately the same refresh grant, with one extra parameter: the identity
 * service re-checks membership and mints a token carrying the new `org_id`.
 * Because the tenant lives in the token, this is the only way to switch -- no
 * client-side state could do it.
 */
export function switchOrganization(
  refreshToken: string,
  organizationId: string,
): Promise<TokenPair> {
  return exchange({
    grant_type: "refresh_token",
    refresh_token: refreshToken,
    organization_id: organizationId,
  });
}
