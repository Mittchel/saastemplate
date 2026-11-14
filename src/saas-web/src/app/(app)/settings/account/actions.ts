"use server";

import { revalidatePath } from "next/cache";

import { api, ApiRequestError } from "@/lib/api";
import { describeError } from "@/lib/error-scenarios";

export interface ProfileState {
  error?: string;
  saved?: boolean;
}

export async function updateProfileAction(
  _previous: ProfileState,
  formData: FormData,
): Promise<ProfileState> {
  const firstName = String(formData.get("firstName") ?? "").trim();
  const lastName = String(formData.get("lastName") ?? "").trim();

  try {
    await api.updateProfile(firstName, lastName);

    // The sidebar footer and the dashboard greeting both read the display name,
    // so the whole signed-in shell has to re-render, not just this page.
    revalidatePath("/", "layout");

    return { saved: true };
  } catch (error) {
    if (error instanceof ApiRequestError) {
      return { error: describeError(error.body, "Could not save your profile.") };
    }
    throw error;
  }
}
