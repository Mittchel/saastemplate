import { ProfileForm } from "@/components/profile-form";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { api } from "@/lib/api";

export const metadata = { title: "Account" };

export default async function AccountPage() {
  const user = await api.me();

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold">Account</h1>
        <p className="text-muted-foreground text-sm">Your profile and access.</p>
      </div>

      <ProfileForm
        firstName={user.firstName}
        lastName={user.lastName}
        email={user.email}
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Workspace</CardTitle>
          <CardDescription>
            {user.organization.name} — you are {user.organizationRole}.
          </CardDescription>
        </CardHeader>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Permissions</CardTitle>
          <CardDescription>
            Granted by your role and carried in your access token.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-1.5">
          {user.permissions.map((permission) => (
            <span
              key={permission}
              className="bg-muted rounded px-1.5 py-0.5 font-mono text-xs"
            >
              {permission}
            </span>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
