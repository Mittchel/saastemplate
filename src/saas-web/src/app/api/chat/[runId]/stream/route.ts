import { auth } from "@/auth";
import { apiUrlInternal } from "@/lib/config";

/**
 * Proxies the agent's token stream from the AI service to the browser.
 *
 * This hop exists for one reason: the access token lives in an httpOnly cookie,
 * so client-side JavaScript cannot set an Authorization header. The browser
 * calls this same-origin route with its cookie, and the server attaches the
 * bearer token.
 *
 * Note this is not the hop the architecture avoids. Routing the stream through
 * the .NET API would add a service that transforms nothing; this one is the
 * browser's own origin and is what keeps the token out of reach of XSS.
 */
export async function GET(
  request: Request,
  { params }: { params: Promise<{ runId: string }> },
) {
  const session = await auth();

  if (!session?.accessToken) {
    return new Response("Unauthorized", { status: 401 });
  }

  const { runId } = await params;

  const upstream = await fetch(`${apiUrlInternal()}/ai/runs/${runId}/stream`, {
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      Accept: "text/event-stream",
    },
    // Propagates the browser closing the tab mid-run, so the agent is not left
    // generating into a stream nobody is reading.
    signal: request.signal,
    cache: "no-store",
  });

  if (!upstream.ok || !upstream.body) {
    return new Response("Could not start the run.", {
      status: upstream.status === 404 ? 404 : 502,
    });
  }

  // Returning the upstream body directly keeps this a pass-through: no
  // buffering, so tokens reach the browser as they are produced.
  return new Response(upstream.body, {
    status: 200,
    headers: {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache, no-transform",
      Connection: "keep-alive",
      "X-Accel-Buffering": "no",
    },
  });
}
