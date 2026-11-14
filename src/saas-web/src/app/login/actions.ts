"use server";

import { AuthError } from "next-auth";
import { redirect } from "next/navigation";

import { signIn } from "@/auth";
import { authIssuerInternal } from "@/lib/config";
import type { ApiErrorBody } from "@/lib/error-scenarios";
import { describeError } from "@/lib/error-scenarios";

export interface AuthFormState {
  error?: string;
}

/**
 * Signs in with the credentials the login form collected.
 *
 * The form posts here rather than to the identity service directly, so the
 * tokens are set as an httpOnly cookie by Auth.js and never reach client-side
 * JavaScript.
 */
export async function loginAction(
  _previous: AuthFormState,
  formData: FormData,
): Promise<AuthFormState> {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  if (!email || !password) {
    return { error: "Enter your email address and password." };
  }

  try {
    await signIn("credentials", { email, password, redirect: false });
  } catch (error) {
    if (error instanceof AuthError) {
      // Deliberately one message for both "no such account" and "wrong
      // password": distinguishing them turns the form into a way to discover
      // which addresses are registered.
      return { error: "That email address or password is incorrect." };
    }
    throw error;
  }

  // Outside the try: redirect() throws a control-flow signal that must not be
  // caught by the handler above.
  redirect("/dashboard");
}

/** Starts the Google OAuth flow. */
export async function googleLoginAction(): Promise<void> {
  await signIn("google", { redirectTo: "/dashboard" });
}

/**
 * Registers a new account and signs straight in.
 *
 * Registration happens on the identity service, which creates the user and
 * their personal workspace in one transaction.
 */
export async function registerAction(
  _previous: AuthFormState,
  formData: FormData,
): Promise<AuthFormState> {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");
  const firstName = String(formData.get("firstName") ?? "").trim();
  const lastName = String(formData.get("lastName") ?? "").trim();

  if (!email || !password) {
    return { error: "Enter your email address and a password." };
  }

  const response = await fetch(`${authIssuerInternal()}/account/register`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password, firstName, lastName }),
    cache: "no-store",
  });

  if (!response.ok) {
    let body: ApiErrorBody | null = null;
    try {
      body = (await response.json()) as ApiErrorBody;
    } catch {
      // Fall through to the generic message below.
    }

    return { error: describeError(body, "Could not create the account.") };
  }

  try {
    await signIn("credentials", { email, password, redirect: false });
  } catch {
    // The account exists; only the automatic sign-in failed.
    return { error: "Account created. Please sign in." };
  }

  redirect("/dashboard");
}

/** Exposed so the login page can hide the Google button when it is unconfigured. */
export async function isGoogleEnabled(): Promise<boolean> {
  return Boolean(process.env.AUTH_GOOGLE_ID);
}
