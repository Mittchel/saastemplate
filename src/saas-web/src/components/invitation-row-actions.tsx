"use client";

import { Trash2Icon } from "lucide-react";
import * as React from "react";
import { toast } from "sonner";

import { revokeAction } from "@/app/(app)/settings/invitations/actions";
import { Button } from "@/components/ui/button";

export function InvitationRowActions({
  invitationId,
  email,
  canManage,
}: {
  invitationId: string;
  email: string;
  canManage: boolean;
}) {
  const [pending, startTransition] = React.useTransition();

  if (!canManage) {
    return null;
  }

  return (
    <Button
      variant="ghost"
      size="icon"
      disabled={pending}
      onClick={() =>
        startTransition(async () => {
          const result = await revokeAction(invitationId);

          if (result?.error) {
            toast.error(result.error);
          } else {
            toast.success(`Invitation to ${email} revoked.`);
          }
        })
      }
      title="Revoke invitation"
    >
      <Trash2Icon className="size-4" />
      <span className="sr-only">Revoke the invitation to {email}</span>
    </Button>
  );
}
