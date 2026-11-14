"use client";

import { Trash2Icon } from "lucide-react";
import * as React from "react";
import { toast } from "sonner";

import { changeRoleAction, removeMemberAction } from "@/app/(app)/settings/members/actions";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

const ROLES = ["Member", "Admin", "Owner"] as const;

export function MemberRowActions({
  userId,
  role,
  fullName,
  email,
  canManage,
  isSelf,
}: {
  userId: string;
  role: string;
  fullName: string;
  email: string;
  canManage: boolean;
  isSelf: boolean;
}) {
  const [pending, startTransition] = React.useTransition();
  const [current, setCurrent] = React.useState(role);

  // Without the permission the row stays informational, matching what the API
  // would allow anyway.
  if (!canManage) {
    return <span className="text-sm">{role}</span>;
  }

  // Base UI emits null when a selection is cleared; there is no "no role", so
  // that case is simply ignored.
  function change(next: string | null) {
    if (!next || next === current) {
      return;
    }

    const previous = current;
    setCurrent(next);

    startTransition(async () => {
      const result = await changeRoleAction(userId, next);

      if (result.error) {
        // Put the control back where it was: the server rejected the change, so
        // leaving the new value showing would be a lie.
        setCurrent(previous);
        toast.error(result.error);
      } else {
        toast.success(`${fullName || email} is now ${next}.`);
      }
    });
  }

  function remove() {
    startTransition(async () => {
      const result = await removeMemberAction(userId);

      if (result.error) {
        toast.error(result.error);
      } else {
        toast.success(`${fullName || email} was removed.`);
      }
    });
  }

  return (
    <div className="flex items-center justify-end gap-2">
      <Select value={current} onValueChange={change} disabled={pending}>
        <SelectTrigger className="w-32" aria-label={`Role for ${email}`}>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {ROLES.map((option) => (
            <SelectItem key={option} value={option}>
              {option}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Button
        variant="ghost"
        size="icon"
        onClick={remove}
        // Leaving is a different operation with different rules (it is allowed
        // for yourself, and the API guards the last owner), so it belongs on the
        // organization page rather than disguised as a removal here.
        disabled={pending || isSelf}
        title={isSelf ? "Leave from the workspace settings instead" : "Remove member"}
      >
        <Trash2Icon className="size-4" />
        <span className="sr-only">Remove {email}</span>
      </Button>
    </div>
  );
}
