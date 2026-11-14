import { notFound } from "next/navigation";

import { MemberRowActions } from "@/components/member-row-actions";
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

export const metadata = { title: "Members" };

export default async function MembersPage() {
  const user = await api.me();

  // The sidebar already hides this route, but a direct navigation must not
  // render a members page for a deployment that has organizations switched off.
  if (!user.features.organizations) {
    notFound();
  }

  // member.read is HR admin only; the API would answer 403. Hide the page
  // instead of failing on a direct navigation.
  if (!user.permissions.includes("member.read")) {
    notFound();
  }

  const members = await api.members(user.organization.id);

  // Mirrors what the API enforces; the controls are hidden rather than shown
  // and then rejected.
  const canManage = user.permissions.includes("member.write");

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold">Members</h1>
        <p className="text-muted-foreground text-sm">
          People with access to {user.organization.name}.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{members.length} member(s)</CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Joined</TableHead>
                <TableHead className="text-right">Role</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {members.map((member) => (
                <TableRow key={member.userId}>
                  <TableCell>{member.fullName || "—"}</TableCell>
                  <TableCell className="text-muted-foreground">{member.email}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {new Date(member.joinedAt).toLocaleDateString()}
                  </TableCell>
                  <TableCell className="text-right">
                    <MemberRowActions
                      userId={member.userId}
                      role={member.role}
                      fullName={member.fullName}
                      email={member.email}
                      canManage={canManage}
                      isSelf={member.userId === user.id}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
