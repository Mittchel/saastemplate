import Link from "next/link";

import { AuthShell } from "@/components/auth-shell";
import { ResetPasswordForm } from "@/components/reset-forms";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata = { title: "Choose a new password" };

export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<{ email?: string; token?: string }>;
}) {
  const { email, token } = await searchParams;

  // The token is validated by the identity service when the form is submitted;
  // this only catches a link that arrived mangled.
  if (!email || !token) {
    return (
      <AuthShell>
        <Card>
          <CardHeader className="text-center">
            <CardTitle className="text-xl">Invalid link</CardTitle>
            <CardDescription>This reset link is incomplete.</CardDescription>
          </CardHeader>
          <CardContent className="text-center">
            <Link href="/forgot-password" className="text-sm underline underline-offset-4">
              Request a new one
            </Link>
          </CardContent>
        </Card>
      </AuthShell>
    );
  }

  return (
    <AuthShell>
      <ResetPasswordForm email={email} token={token} />
    </AuthShell>
  );
}
