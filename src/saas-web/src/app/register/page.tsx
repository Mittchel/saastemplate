import { GalleryVerticalEndIcon } from "lucide-react";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";

import { auth } from "@/auth";
import { RegisterForm } from "@/components/register-form";
import { fetchFeatures } from "@/lib/api";

export const metadata = { title: "Sign up" };

export default async function RegisterPage() {
  const session = await auth();

  if (session?.accessToken && !session.error) {
    redirect("/dashboard");
  }

  // Invite-only deployments should not render a sign-up screen at all.
  const features = await fetchFeatures();
  if (!features.publicSignup) {
    notFound();
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
        <RegisterForm />
      </div>
    </div>
  );
}
