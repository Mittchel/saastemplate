"use server";

import { api, ApiRequestError } from "@/lib/api";
import { describeError } from "@/lib/error-scenarios";

/**
 * Creates a run so the browser can open its stream.
 *
 * Authorization lives on the .NET side; this just translates a failure into
 * something the chat UI can show.
 */
export async function createRunAction(
  input: string,
  threadId: string | null,
): Promise<{ id: string; threadId: string } | { error: string }> {
  try {
    const run = await api.createRun(input, threadId ?? undefined);
    return { id: run.id, threadId: run.threadId };
  } catch (error) {
    if (error instanceof ApiRequestError) {
      return { error: describeError(error.body, "Could not start the run.") };
    }
    throw error;
  }
}
