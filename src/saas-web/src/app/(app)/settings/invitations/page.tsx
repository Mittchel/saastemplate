import { notFound } from "next/navigation";

import { InvitationRowActions } from "@/components/invitation-row-actions";
import { InviteForm } from "@/components/invite-form";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api } from "@/lib/api";

export const metadata = { title: "Invitations" };

export default async function InvitationsPage() {
  const user = await api.me();

  if (!user.features.organizations) {
    notFound();
  }

  const invitations = await api.invitations(user.organization.id);
  const canManage = user.permissions.includes("member.invite");

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold">Invitations</h1>
        <p className="text-muted-foreground text-sm">
          Invite people into {user.organization.name}.
        </p>
      </div>

      <InviteForm />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Pending and past</CardTitle>
        </CardHeader>
        <CardContent>
          {invitations.length === 0 ? (
            <p className="text-muted-foreground text-sm">No invitations yet.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Email</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead className="w-10" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {invitations.map((invitation) => (
                  <TableRow key={invitation.id}>
                    <TableCell>{invitation.email}</TableCell>
                    <TableCell>{invitation.role}</TableCell>
                    <TableCell>
                      {invitation.acceptedAt
                        ? "Accepted"
                        : invitation.isPending
                          ? "Pending"
                          : "Expired"}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {new Date(invitation.expiresAt).toLocaleDateString()}
                    </TableCell>
                    <TableCell className="text-right">
                      {invitation.isPending ? (
                        <InvitationRowActions
                          invitationId={invitation.id}
                          email={invitation.email}
                          canManage={canManage}
                        />
                      ) : null}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
