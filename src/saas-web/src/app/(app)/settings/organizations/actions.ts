"use server";

import { revalidatePath } from "next/cache";

import { api, ApiRequestError } from "@/lib/api";
import { describeError } from "@/lib/error-scenarios";

export interface CreateOrgState {
  error?: string;
  created?: string;
}

export async function createOrganizationAction(
  _previous: CreateOrgState,
  formData: FormData,
): Promise<CreateOrgState> {
  const name = String(formData.get("name") ?? "").trim();

  if (!name) {
    return { error: "Enter a workspace name." };
  }

  try {
    const organization = await api.createOrganization(name);
    revalidatePath("/settings/organizations");

    // Switching into it is a token operation, so it happens in the sidebar
    // switcher rather than here.
    return { created: organization.name };
  } catch (error) {
    if (error instanceof ApiRequestError) {
      return { error: describeError(error.body, "Could not create the workspace.") };
    }
    throw error;
  }
}
