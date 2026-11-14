import { notFound } from "next/navigation";

import { PlatformOrganizations } from "@/components/platform-organizations";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";

export const metadata = { title: "Platform" };

export default async function PlatformPage() {
  const user = await api.me();

  // The API refuses anyway; 404 rather than 403 so the route does not confirm
  // that an operator area exists to someone who is not one.
  if (!user.isPlatformAdmin) {
    notFound();
  }

  const organizations = await api.platformOrganizations();

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold">Platform</h1>
        <p className="text-muted-foreground text-sm">
          Every workspace on this deployment. Entering one scopes you to it like
          any other member — there is no cross-tenant view of customer data.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {organizations.length} workspace{organizations.length === 1 ? "" : "s"}
          </CardTitle>
          <CardDescription>
            Counts only. To see inside one, enter it.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <PlatformOrganizations
            organizations={organizations}
            activeOrganizationId={user.organization.id}
          />
        </CardContent>
      </Card>
    </div>
  );
}
