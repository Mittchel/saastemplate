import { notFound } from "next/navigation";

import { CreateOrganizationForm } from "@/components/create-organization-form";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";

export const metadata = { title: "Workspaces" };

export default async function OrganizationsPage() {
  const user = await api.me();

  if (!user.features.organizations) {
    notFound();
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold">Workspaces</h1>
        <p className="text-muted-foreground text-sm">
          Switch between workspaces from the sidebar.
        </p>
      </div>

      <CreateOrganizationForm />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Your workspaces</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {user.organizations.map((organization) => (
            <div key={organization.id} className="flex items-center justify-between">
              <span>
                {organization.name}
                {organization.isPersonal ? (
                  <span className="text-muted-foreground"> · personal</span>
                ) : null}
              </span>
              <span className="text-muted-foreground">{organization.role}</span>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
