import { Chat } from "@/components/chat";
import { api } from "@/lib/api";
import { loadThread } from "@/lib/threads";

export const metadata = { title: "Chat" };

export default async function ChatPage() {
  // Resume the most recent conversation, so the page is not empty for someone
  // coming back to it. The runs list is tenant-scoped, so this can only ever
  // pick up a thread the caller is entitled to see.
  const recentRuns = await api.runs();
  const threadId = recentRuns[0]?.threadId ?? null;
  const history = threadId ? await loadThread(threadId) : [];

  return (
    <div className="flex flex-col gap-2">
      <div>
        <h1 className="text-lg font-semibold">Chat</h1>
        <p className="text-muted-foreground text-sm">
          A LangGraph agent, streaming over SSE, with one tool that calls the API as you.
        </p>
      </div>
      <Chat initialThreadId={threadId} initialMessages={history} />
    </div>
  );
}
