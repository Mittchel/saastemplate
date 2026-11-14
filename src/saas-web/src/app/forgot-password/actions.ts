"use server";

import { authIssuerInternal } from "@/lib/config";
import type { ApiErrorBody } from "@/lib/error-scenarios";
import { describeError } from "@/lib/error-scenarios";

export interface ResetState {
  error?: string;
  sent?: boolean;
  done?: boolean;
}

/**
 * Requests a reset link.
 *
 * Reports success whatever the server says, because the server deliberately
 * answers identically for registered and unregistered addresses. Showing
 * "no such account" here would undo that.
 */
export async function forgotPasswordAction(
  _previous: ResetState,
  formData: FormData,
): Promise<ResetState> {
  const email = String(formData.get("email") ?? "").trim();

  if (!email) {
    return { error: "Enter your email address." };
  }

  await fetch(`${authIssuerInternal()}/account/forgot-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email }),
    cache: "no-store",
  }).catch(() => undefined);

  return { sent: true };
}

/** Completes a reset with the token from the emailed link. */
export async function resetPasswordAction(
  _previous: ResetState,
  formData: FormData,
): Promise<ResetState> {
  const email = String(formData.get("email") ?? "");
  const token = String(formData.get("token") ?? "");
  const newPassword = String(formData.get("newPassword") ?? "");
  const confirm = String(formData.get("confirmPassword") ?? "");

  if (!newPassword) {
    return { error: "Choose a new password." };
  }

  // Checked here rather than server-side: it is a typo guard, not a security
  // rule, and the server has no business knowing the password twice.
  if (newPassword !== confirm) {
    return { error: "The two passwords do not match." };
  }

  const response = await fetch(`${authIssuerInternal()}/account/reset-password`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, token, newPassword }),
    cache: "no-store",
  });

  if (!response.ok) {
    let body: ApiErrorBody | null = null;
    try {
      body = (await response.json()) as ApiErrorBody;
    } catch {
      // Fall through to the generic message.
    }

    return { error: describeError(body, "Could not reset the password.") };
  }

  return { done: true };
}
