import { AuthShell } from "@/components/auth-shell";
import { ForgotPasswordForm } from "@/components/reset-forms";

export const metadata = { title: "Reset password" };

export default function ForgotPasswordPage() {
  return (
    <AuthShell>
      <ForgotPasswordForm />
    </AuthShell>
  );
}
