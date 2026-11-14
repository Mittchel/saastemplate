import { cookies } from "next/headers";

/**
 * The Auth.js CSRF token, for forms that post to its endpoints directly.
 *
 * Auth.js rejects a POST to `/api/auth/signout` that carries no `csrfToken`
 * field, redirecting to `/login?error=MissingCSRF` instead of signing the user
 * out. Its own client helpers fetch the token for you; a plain HTML form has to
 * include it, which is the price of having sign-out work without JavaScript.
 *
 * The cookie value is `<token>|<hash>` — only the token half goes in the form.
 */
export async function getCsrfToken(): Promise<string | null> {
  const store = await cookies();

  // Auth.js prefixes the cookie with __Host- when it is served over https.
  const cookie =
    store.get("authjs.csrf-token") ?? store.get("__Host-authjs.csrf-token");

  if (!cookie?.value) {
    return null;
  }

  return cookie.value.split("|")[0] ?? null;
}
