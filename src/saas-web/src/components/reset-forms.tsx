"use client";

import Link from "next/link";
import { useActionState } from "react";

import {
  type ResetState,
  forgotPasswordAction,
  resetPasswordAction,
} from "@/app/forgot-password/actions";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";

const INITIAL: ResetState = {};

export function ForgotPasswordForm() {
  const [state, formAction, isPending] = useActionState(forgotPasswordAction, INITIAL);

  // Deliberately the same confirmation whether or not the address exists -- the
  // server answers identically, and so must this.
  if (state.sent) {
    return (
      <Card>
        <CardHeader className="text-center">
          <CardTitle className="text-xl">Check your email</CardTitle>
          <CardDescription>
            If that address has an account, a reset link is on its way.
          </CardDescription>
        </CardHeader>
        <CardContent className="text-center">
          <Link href="/login" className="text-sm underline underline-offset-4">
            Back to sign in
          </Link>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Reset your password</CardTitle>
        <CardDescription>We will email you a link.</CardDescription>
      </CardHeader>
      <CardContent>
        <form action={formAction}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="email">Email</FieldLabel>
              <Input id="email" name="email" type="email" autoComplete="email" required />
            </Field>

            {state.error ? (
              <p role="alert" className="text-destructive text-sm">
                {state.error}
              </p>
            ) : null}

            <Field>
              <Button type="submit" disabled={isPending}>
                {isPending ? "Sending…" : "Send reset link"}
              </Button>
              <FieldDescription className="text-center">
                <Link href="/login">Back to sign in</Link>
              </FieldDescription>
            </Field>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  );
}

export function ResetPasswordForm({ email, token }: { email: string; token: string }) {
  const [state, formAction, isPending] = useActionState(resetPasswordAction, INITIAL);

  if (state.done) {
    return (
      <Card>
        <CardHeader className="text-center">
          <CardTitle className="text-xl">Password changed</CardTitle>
          <CardDescription>You can sign in with your new password.</CardDescription>
        </CardHeader>
        <CardContent className="text-center">
          <Link href="/login" className="text-sm underline underline-offset-4">
            Go to sign in
          </Link>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader className="text-center">
        <CardTitle className="text-xl">Choose a new password</CardTitle>
        <CardDescription>{email}</CardDescription>
      </CardHeader>
      <CardContent>
        <form action={formAction}>
          {/* Carried through the form so the action has them without a round trip. */}
          <input type="hidden" name="email" value={email} />
          <input type="hidden" name="token" value={token} />

          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="newPassword">New password</FieldLabel>
              <Input
                id="newPassword"
                name="newPassword"
                type="password"
                autoComplete="new-password"
                required
              />
              <FieldDescription>
                At least 10 characters, with an uppercase letter and a digit.
              </FieldDescription>
            </Field>

            <Field>
              <FieldLabel htmlFor="confirmPassword">Confirm password</FieldLabel>
              <Input
                id="confirmPassword"
                name="confirmPassword"
                type="password"
                autoComplete="new-password"
                required
              />
            </Field>

            {state.error ? (
              <p role="alert" className="text-destructive text-sm">
                {state.error}
              </p>
            ) : null}

            <Field>
              <Button type="submit" disabled={isPending}>
                {isPending ? "Saving…" : "Change password"}
              </Button>
            </Field>
          </FieldGroup>
        </form>
      </CardContent>
    </Card>
  );
}
