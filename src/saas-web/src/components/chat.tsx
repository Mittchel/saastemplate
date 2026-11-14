"use client";

import { SendIcon } from "lucide-react";
import * as React from "react";

import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { createRunAction } from "@/app/(app)/chat/actions";

interface Message {
  id: string;
  role: "user" | "assistant";
  text: string;
  /** Tool names the agent called while producing this message. */
  tools?: string[];
}

export function Chat({
  initialThreadId = null,
  initialMessages = [],
}: {
  initialThreadId?: string | null;
  initialMessages?: { role: "user" | "assistant"; text: string }[];
}) {
  const [messages, setMessages] = React.useState<Message[]>(() =>
    // Seeded from the checkpointer so a reload does not lose the transcript.
    initialMessages.map((message) => ({
      id: crypto.randomUUID(),
      role: message.role,
      text: message.text,
    })),
  );
  const [input, setInput] = React.useState("");
  const [isStreaming, setIsStreaming] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  // Carried across turns so the checkpointer continues the same conversation
  // rather than starting a new one each time.
  const threadId = React.useRef<string | null>(initialThreadId);
  const scrollRef = React.useRef<HTMLDivElement>(null);

  React.useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight });
  }, [messages]);

  async function send(event: React.FormEvent) {
    event.preventDefault();

    const text = input.trim();
    if (!text || isStreaming) {
      return;
    }

    setInput("");
    setError(null);
    setIsStreaming(true);

    const userMessage: Message = { id: crypto.randomUUID(), role: "user", text };
    const assistantId = crypto.randomUUID();

    setMessages((previous) => [
      ...previous,
      userMessage,
      { id: assistantId, role: "assistant", text: "", tools: [] },
    ]);

    try {
      // Two steps on purpose: the .NET API authorises and persists the run
      // before any model tokens are spent, and only then does the stream open.
      const run = await createRunAction(text, threadId.current);

      if ("error" in run) {
        setError(run.error);
        setMessages((previous) => previous.filter((m) => m.id !== assistantId));
        return;
      }

      threadId.current = run.threadId;

      await consume(`/api/chat/${run.id}/stream`, assistantId);
    } catch {
      setError("Could not reach the assistant.");
    } finally {
      setIsStreaming(false);
    }
  }

  /** Reads the SSE stream and appends tokens as they arrive. */
  async function consume(url: string, assistantId: string) {
    const response = await fetch(url, { headers: { Accept: "text/event-stream" } });

    if (!response.ok || !response.body) {
      setError("The assistant could not start.");
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });

      // SSE frames are separated by a blank line; a partial frame stays in the
      // buffer until the rest of it arrives.
      const frames = buffer.split("\n\n");
      buffer = frames.pop() ?? "";

      for (const frame of frames) {
        const eventName = frame.match(/^event:\s*(.*)$/m)?.[1]?.trim();
        const dataLine = frame.match(/^data:\s*(.*)$/m)?.[1];

        if (!eventName || !dataLine) {
          continue;
        }

        let payload: Record<string, string> = {};
        try {
          payload = JSON.parse(dataLine);
        } catch {
          continue;
        }

        if (eventName === "token") {
          setMessages((previous) =>
            previous.map((message) =>
              message.id === assistantId
                ? { ...message, text: message.text + (payload.text ?? "") }
                : message,
            ),
          );
        } else if (eventName === "tool") {
          setMessages((previous) =>
            previous.map((message) =>
              message.id === assistantId
                ? { ...message, tools: [...(message.tools ?? []), payload.name] }
                : message,
            ),
          );
        } else if (eventName === "error") {
          setError(payload.message ?? "The assistant failed.");
        }
      }
    }
  }

  return (
    <div className="flex h-[calc(100svh-8rem)] flex-col gap-4">
      <div ref={scrollRef} className="flex-1 space-y-4 overflow-y-auto pr-2">
        {messages.length === 0 ? (
          <p className="text-muted-foreground py-12 text-center text-sm">
            Ask the assistant something. Try &ldquo;who am I?&rdquo; to see it call back
            into the API on your behalf.
          </p>
        ) : null}

        {messages.map((message) => (
          <div
            key={message.id}
            className={
              message.role === "user"
                ? "bg-primary text-primary-foreground ml-auto w-fit max-w-[80%] rounded-lg px-3 py-2 text-sm"
                : "bg-muted w-fit max-w-[80%] rounded-lg px-3 py-2 text-sm whitespace-pre-wrap"
            }
          >
            {message.tools && message.tools.length > 0 ? (
              <p className="text-muted-foreground mb-1 text-xs">
                called {message.tools.join(", ")}
              </p>
            ) : null}
            {message.text || (message.role === "assistant" && isStreaming ? "…" : "")}
          </div>
        ))}
      </div>

      {error ? (
        <p role="alert" className="text-destructive text-sm">
          {error}
        </p>
      ) : null}

      <form onSubmit={send} className="flex items-end gap-2">
        <Textarea
          value={input}
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              void send(event);
            }
          }}
          placeholder="Send a message…"
          rows={2}
          className="resize-none"
          disabled={isStreaming}
        />
        <Button type="submit" disabled={isStreaming || !input.trim()} size="icon">
          <SendIcon className="size-4" />
          <span className="sr-only">Send</span>
        </Button>
      </form>
    </div>
  );
}
