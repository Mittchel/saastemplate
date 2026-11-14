import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";

export const metadata = { title: "Dashboard" };

export default async function DashboardPage() {
  const user = await api.me();

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold">
          Welcome back, {user.fullName || user.email}
        </h1>
        <p className="text-muted-foreground text-sm">
          You are working in {user.organization.name}
          {user.organizationRole ? ` as ${user.organizationRole}` : ""}.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardDescription>Workspace</CardDescription>
            <CardTitle className="text-xl">{user.organization.name}</CardTitle>
          </CardHeader>
          <CardContent className="text-muted-foreground text-sm">
            {user.organization.isPersonal ? "Personal workspace" : "Team workspace"}
          </CardContent>
        </Card>

        {user.features.organizations ? (
          <Card>
            <CardHeader>
              <CardDescription>Workspaces</CardDescription>
              <CardTitle className="text-xl">{user.organizations.length}</CardTitle>
            </CardHeader>
            <CardContent className="text-muted-foreground text-sm">
              Switch between them in the sidebar.
            </CardContent>
          </Card>
        ) : null}

      </div>
    </div>
  );
}
