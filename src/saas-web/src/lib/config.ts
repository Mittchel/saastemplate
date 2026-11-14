/**
 * Service URLs.
 *
 * Server-side code reaches the other services over the container network;
 * the browser can only use the public hostnames. Getting these the wrong way
 * round produces failures that only show up inside Docker, so they are named
 * explicitly rather than inferred.
 */

function required(name: string, value: string | undefined): string {
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value.replace(/\/$/, "");
}

/** Identity service, as reached from the Next.js server. */
export const authIssuerInternal = () =>
  required("AUTH_ISSUER_INTERNAL", process.env.AUTH_ISSUER_INTERNAL);

/** Resource API, as reached from the Next.js server. */
export const apiUrlInternal = () =>
  required("API_URL_INTERNAL", process.env.API_URL_INTERNAL);

/** AI service, as reached from the Next.js server. */
export const aiUrlInternal = () =>
  (process.env.AI_URL_INTERNAL ?? "http://ai:8000").replace(/\/$/, "");

/** Resource API, as reached from the browser. Used for the SSE stream. */
export const apiUrlPublic = () =>
  (process.env.NEXT_PUBLIC_API_URL ?? "http://api.localhost").replace(/\/$/, "");

export const oidcClientId = () => process.env.OIDC_CLIENT_ID ?? "saas-web";

export const oidcClientSecret = () => required("OIDC_CLIENT_SECRET", process.env.OIDC_CLIENT_SECRET);

/** Whether to render the Google button on the login form. */
export const googleEnabled = () => Boolean(process.env.AUTH_GOOGLE_ID);

/**
 * Scopes requested at sign-in.
 *
 * `offline_access` is not optional: the refresh token it returns is both how a
 * session is renewed and how the active organization is switched.
 */
export const OIDC_SCOPES = "openid email profile roles offline_access saas.api";
