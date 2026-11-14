"use server";

import { revalidatePath } from "next/cache";

import { api, ApiRequestError } from "@/lib/api";
import { describeError } from "@/lib/error-scenarios";

export interface MemberActionResult {
  error?: string;
}

export async function changeRoleAction(
  userId: string,
  role: string,
): Promise<MemberActionResult> {
  try {
    const me = await api.me();
    await api.updateMemberRole(me.organization.id, userId, role);

    revalidatePath("/settings/members");
    return {};
  } catch (error) {
    if (error instanceof ApiRequestError) {
      // The API refuses to demote the last HR admin; surfacing its reason is more
      // useful than a generic failure.
      return { error: describeError(error.body, "Could not change the role.") };
    }
    throw error;
  }
}

export async function removeMemberAction(userId: string): Promise<MemberActionResult> {
  try {
    const me = await api.me();
    await api.removeMember(me.organization.id, userId);

    revalidatePath("/settings/members");
    return {};
  } catch (error) {
    if (error instanceof ApiRequestError) {
      return { error: describeError(error.body, "Could not remove the member.") };
    }
    throw error;
  }
}
