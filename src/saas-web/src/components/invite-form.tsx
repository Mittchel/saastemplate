"use client";

import { useActionState } from "react";

import { type InviteState, inviteAction } from "@/app/(app)/settings/invitations/actions";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { roleLabel } from "@/lib/roles";

const INITIAL: InviteState = {};

export function InviteForm() {
  const [state, formAction, isPending] = useActionState(inviteAction, INITIAL);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Invite someone</CardTitle>
        <CardDescription>
          Pending invitations count against the plan&apos;s seat limit.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form action={formAction} className="flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex-1 space-y-1.5">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              name="email"
              type="email"
              placeholder="teammate@example.com"
              required
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="role">Role</Label>
            <Select name="role" defaultValue="Employee">
              <SelectTrigger id="role" className="sm:w-36">
                <SelectValue>{(value: string) => roleLabel(value)}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Employee">Employee</SelectItem>
                <SelectItem value="Manager">Manager</SelectItem>
                <SelectItem value="HrAdmin">HR admin</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <Button type="submit" disabled={isPending}>
            {isPending ? "Inviting…" : "Send invite"}
          </Button>
        </form>

        {state.error ? (
          <p role="alert" className="text-destructive mt-3 text-sm">
            {state.error}
          </p>
        ) : null}

        {state.inviteLink ? (
          <div className="bg-muted mt-3 rounded-md p-3 text-sm">
            <p className="mb-1 font-medium">Invitation created</p>
            <p className="text-muted-foreground">
              No email provider is configured in the template, so send this link yourself:
            </p>
            <code className="mt-1 block break-all font-mono text-xs">{state.inviteLink}</code>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}
