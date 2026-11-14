"use server";

import { revalidatePath } from "next/cache";

import { api, ApiRequestError } from "@/lib/api";
import { describeError } from "@/lib/error-scenarios";

export interface InviteState {
  error?: string;
  /** Present on success: the link to send, since no mail provider is wired up. */
  inviteLink?: string;
}

export async function inviteAction(
  _previous: InviteState,
  formData: FormData,
): Promise<InviteState> {
  const email = String(formData.get("email") ?? "").trim();
  const role = String(formData.get("role") ?? "Employee");

  if (!email) {
    return { error: "Enter an email address." };
  }

  try {
    const user = await api.me();
    const created = await api.createInvitation(user.organization.id, email, role);

    revalidatePath("/settings/invitations");

    // The template has no email provider on purpose, so the link is surfaced
    // here for the inviter to send. Wire a provider and this goes away.
    return { inviteLink: `/invitations/accept?token=${created.token}` };
  } catch (error) {
    if (error instanceof ApiRequestError) {
      return { error: describeError(error.body, "Could not create the invitation.") };
    }
    throw error;
  }
}

export async function revokeAction(
  invitationId: string,
): Promise<{ error?: string } | void> {
  try {
    const user = await api.me();
    await api.revokeInvitation(user.organization.id, invitationId);

    revalidatePath("/settings/invitations");
  } catch (error) {
    if (error instanceof ApiRequestError) {
      return { error: describeError(error.body, "Could not revoke the invitation.") };
    }
    throw error;
  }
}
