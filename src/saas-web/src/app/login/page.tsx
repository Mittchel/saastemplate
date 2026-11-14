import { GalleryVerticalEndIcon } from "lucide-react";
import Link from "next/link";
import { redirect } from "next/navigation";

import { auth } from "@/auth";
import { LoginForm } from "@/components/login-form";
import { googleEnabled } from "@/lib/config";

/**
 * A server component so the Google button's availability and the already-signed-in
 * check are both settled before anything renders.
 */
export default async function LoginPage() {
  const session = await auth();

  if (session?.accessToken && !session.error) {
    redirect("/dashboard");
  }

  return (
    <div className="bg-muted flex min-h-svh flex-col items-center justify-center gap-6 p-6 md:p-10">
      <div className="flex w-full max-w-sm flex-col gap-6">
        <Link href="/" className="flex items-center gap-2 self-center font-medium">
          <div className="bg-primary text-primary-foreground flex size-6 items-center justify-center rounded-md">
            <GalleryVerticalEndIcon className="size-4" />
          </div>
          Saas
        </Link>
        <LoginForm googleEnabled={googleEnabled()} />
      </div>
    </div>
  );
}
