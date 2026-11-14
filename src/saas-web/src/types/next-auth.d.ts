import "next-auth";
import "next-auth/jwt";

/**
 * The token pair lives on the session so server components can forward the
 * access token to the API, and on the JWT so the callbacks can renew it.
 */
declare module "next-auth" {
  interface Session {
    accessToken?: string;
    organizationId?: string;
    organizationRole?: string;
    /** Set when renewal or an organization switch failed. */
    error?: string;
  }
}

declare module "next-auth/jwt" {
  interface JWT {
    accessToken?: string;
    refreshToken?: string;
    /** Epoch milliseconds. */
    expiresAt?: number;
    organizationId?: string;
    organizationRole?: string | null;
    error?: string;
  }
}
