"use server";

import { revalidatePath } from "next/cache";

export interface EnterState {
  error?: string;
}

/**
 * Nothing happens here.
 *
 * Entering a workspace is a token operation: the browser asks the identity
 * service for a token scoped to the target organization, which re-checks that
 * the caller is a platform admin before issuing one. A server action could not
 * do it without duplicating that exchange, so the switch runs client-side
 * through the same Auth.js session update the workspace switcher uses.
 */
export async function refreshAfterEnterAction(): Promise<void> {
  revalidatePath("/", "layout");
}
