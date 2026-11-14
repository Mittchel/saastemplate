"use client";

import { LogInIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useSession } from "next-auth/react";
import * as React from "react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { PlatformOrganization } from "@/lib/api";

/**
 * Every tenant, with a way into each.
 *
 * "Enter" reuses the ordinary workspace switch: the identity service re-checks
 * the operator role and mints a token scoped to that organization. The operator
 * then sees exactly what a member sees, because they genuinely are one for the
 * life of that token -- there is no god-mode view that reads across tenants.
 */
export function PlatformOrganizations({
  organizations,
  activeOrganizationId,
}: {
  organizations: PlatformOrganization[];
  activeOrganizationId: string;
}) {
  const router = useRouter();
  const { update } = useSession();
  const [pending, startTransition] = React.useTransition();

  function enter(organization: PlatformOrganization) {
    startTransition(async () => {
      try {
        await update({ organizationId: organization.id });
        toast.success(`Entered ${organization.name}.`);
        router.push("/dashboard");
        router.refresh();
      } catch {
        toast.error(`Could not enter ${organization.name}.`);
      }
    });
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Workspace</TableHead>
          <TableHead>Type</TableHead>
          <TableHead className="text-right">Members</TableHead>
          <TableHead className="text-right">Runs</TableHead>
          <TableHead>Created</TableHead>
          <TableHead className="w-28" />
        </TableRow>
      </TableHeader>
      <TableBody>
        {organizations.map((organization) => (
          <TableRow key={organization.id}>
            <TableCell className="font-medium">{organization.name}</TableCell>
            <TableCell className="text-muted-foreground">
              {organization.isPersonal ? "Personal" : "Team"}
            </TableCell>
            <TableCell className="text-right">{organization.memberCount}</TableCell>
            <TableCell className="text-right">{organization.runCount}</TableCell>
            <TableCell className="text-muted-foreground">
              {new Date(organization.createdDate).toLocaleDateString()}
            </TableCell>
            <TableCell className="text-right">
              {organization.id === activeOrganizationId ? (
                <span className="text-muted-foreground text-xs">Current</span>
              ) : (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={pending}
                  onClick={() => enter(organization)}
                >
                  <LogInIcon className="size-3.5" />
                  Enter
                </Button>
              )}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
