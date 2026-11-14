import Link from "next/link";
import { redirect } from "next/navigation";

import { auth } from "@/auth";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

import { apiUrlInternal } from "@/lib/config";
import { describeError } from "@/lib/error-scenarios";
import type { ApiErrorBody } from "@/lib/error-scenarios";

export const metadata = { title: "Accept invitation" };

/**
 * Redeems an invitation for the signed-in user.
 *
 * The API matches the token against the caller's own email address, so a
 * forwarded link cannot be redeemed by whoever happens to receive it.
 */
export default async function AcceptInvitationPage({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;
  const session = await auth();

  if (!token) {
    return <Result title="Invalid link" detail="This invitation link is missing its token." />;
  }

  if (!session?.accessToken) {
    // Sign in first, then come back to this exact link.
    redirect(`/login?callbackUrl=/invitations/accept?token=${encodeURIComponent(token)}`);
  }

  const response = await fetch(`${apiUrlInternal()}/invitations/accept`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${session.accessToken}`,
    },
    body: JSON.stringify({ token }),
    cache: "no-store",
  });

  if (!response.ok) {
    let body: ApiErrorBody | null = null;
    try {
      body = (await response.json()) as ApiErrorBody;
    } catch {
      // Fall through to the generic message.
    }

    return (
      <Result
        title="Could not accept"
        detail={describeError(body, "This invitation could not be accepted.")}
      />
    );
  }

  const organization = (await response.json()) as { name: string };

  return (
    <Result
      title={`You have joined ${organization.name}`}
      detail="Switch to it from the workspace menu in the sidebar."
    />
  );
}

function Result({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="bg-muted flex min-h-svh items-center justify-center p-6">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle className="text-lg">{title}</CardTitle>
          <CardDescription>{detail}</CardDescription>
        </CardHeader>
        <CardContent>
          <Link href="/dashboard" className="text-sm underline underline-offset-4">
            Go to the dashboard
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
