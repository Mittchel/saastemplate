import { auth } from "@/auth";
import { aiUrlInternal } from "@/lib/config";

export interface ThreadMessage {
  role: "user" | "assistant";
  text: string;
}

/**
 * Replays a conversation from the AI service.
 *
 * Server-side only, carrying the caller's access token: the AI service checks
 * the thread against the .NET API, which scopes it by tenant.
 */
export async function loadThread(threadId: string): Promise<ThreadMessage[]> {
  const session = await auth();

  if (!session?.accessToken) {
    return [];
  }

  const response = await fetch(`${aiUrlInternal()}/threads/${threadId}/messages`, {
    headers: { Authorization: `Bearer ${session.accessToken}` },
    cache: "no-store",
  });

  if (!response.ok) {
    // A thread that cannot be read is simply not restored; the user gets an
    // empty composer rather than an error for something they did not ask for.
    return [];
  }

  const body = (await response.json()) as { messages: ThreadMessage[] };

  return body.messages ?? [];
}
