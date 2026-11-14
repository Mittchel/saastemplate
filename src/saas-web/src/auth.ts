/**
 * Auth.js configuration.
 *
 * The session is a JWT cookie holding the token pair the identity service
 * issued. The access token is what every call to the .NET API and the AI
 * service carries, so this file is the only place tokens are obtained,
 * renewed, or exchanged for a different organization.
 */

import NextAuth from "next-auth";
import type { Provider } from "next-auth/providers";
import Credentials from "next-auth/providers/credentials";
import Google from "next-auth/providers/google";

import { googleEnabled } from "@/lib/config";
import {
  TokenError,
  renew,
  signInWithExternal,
  signInWithPassword,
  switchOrganization,
} from "@/lib/tokens";

/** Renew this long before expiry, so a request never races the clock. */
const RENEW_SKEW_MS = 60_000;

// Typed explicitly: without it the array is inferred as credentials-only and
// pushing the OAuth provider below is a type error.
const providers: Provider[] = [
  Credentials({
    id: "credentials",
    name: "Email and password",
    credentials: {
      email: { label: "Email", type: "email" },
      password: { label: "Password", type: "password" },
    },
    async authorize(credentials) {
      const email = credentials?.email;
      const password = credentials?.password;

      if (typeof email !== "string" || typeof password !== "string") {
        return null;
      }

      try {
        const tokens = await signInWithPassword(email, password);

        // Returned fields are picked up by the jwt callback below; Auth.js
        // requires an `id`.
        return {
          id: email,
          email,
          ...tokens,
        } as never;
      } catch (error) {
        if (error instanceof TokenError) {
          // Returning null makes Auth.js report a generic CredentialsSignin
          // error, which is what we want -- a distinguishable "no such user"
          // would let the form enumerate registered addresses.
          return null;
        }
        throw error;
      }
    },
  }),
];

// Only offered when actually configured, so the login form does not show a
// button that cannot work.
if (googleEnabled()) {
  // Account linking is handled by the identity service, which only links a
  // provider identity to an existing account when the provider says the email
  // address is verified. Auth.js own linking setting is inert here anyway --
  // it applies to database adapters, and this app uses the JWT strategy.
  providers.push(Google);
}

export const { handlers, signIn, signOut, auth, unstable_update } = NextAuth({
  providers,
  session: { strategy: "jwt" },
  pages: {
    signIn: "/login",
  },
  callbacks: {
    async jwt({ token, user, account, trigger, session }) {
      // 1a. Social sign-in. The provider authenticated the user; trade its ID
      //     token for ours so the rest of the app sees one kind of session.
      //     Checked before `user`, because on this path `user` is the Google
      //     profile and carries no tokens of ours.
      if (account?.provider && account.provider !== "credentials") {
        const idToken = account.id_token;

        if (typeof idToken !== "string") {
          return { ...token, error: "ExternalLoginFailed" };
        }

        try {
          const exchanged = await signInWithExternal(account.provider, idToken);
          return { ...token, ...exchanged, error: undefined };
        } catch {
          // Surfaced as a signed-out state rather than a session with no token,
          // which the app layout would bounce back to /login forever.
          return { ...token, error: "ExternalLoginFailed" };
        }
      }

      // 1b. Password sign-in: copy the freshly issued pair onto the session token.
      if (user) {
        const signedIn = user as unknown as {
          accessToken: string;
          refreshToken: string;
          expiresAt: number;
          organizationId: string;
          organizationRole: string;
        };

        return {
          ...token,
          accessToken: signedIn.accessToken,
          refreshToken: signedIn.refreshToken,
          expiresAt: signedIn.expiresAt,
          organizationId: signedIn.organizationId,
          organizationRole: signedIn.organizationRole,
        };
      }

      // 2. Organization switch, triggered by the sidebar switcher calling
      //    update({ organizationId }). The identity service re-validates
      //    membership before issuing the new token.
      const requestedOrganization = (session as { organizationId?: string } | undefined)
        ?.organizationId;

      if (trigger === "update" && requestedOrganization && token.refreshToken) {
        try {
          const switched = await switchOrganization(
            token.refreshToken as string,
            requestedOrganization,
          );
          return { ...token, ...switched };
        } catch {
          // Leaving the token untouched keeps the user signed in to the
          // organization they were already in.
          return { ...token, error: "OrganizationSwitchFailed" };
        }
      }

      // 3. Still valid: nothing to do.
      if (typeof token.expiresAt === "number" && Date.now() < token.expiresAt - RENEW_SKEW_MS) {
        return token;
      }

      // 4. Expiring: renew.
      if (!token.refreshToken) {
        return { ...token, error: "RefreshTokenMissing" };
      }

      try {
        const renewed = await renew(token.refreshToken as string);
        return { ...token, ...renewed, error: undefined };
      } catch {
        // Surfaced to the UI as a signed-out state rather than a silent 401
        // loop against the API.
        return { ...token, error: "RefreshFailed" };
      }
    },

    async session({ session, token }) {
      session.accessToken = token.accessToken as string;
      session.organizationId = token.organizationId as string;
      session.organizationRole = token.organizationRole as string;
      session.error = token.error as string | undefined;

      return session;
    },
  },
});
