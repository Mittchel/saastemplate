import { redirect } from "next/navigation";

import { AppSidebar } from "@/components/app-sidebar";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { auth } from "@/auth";
import { api } from "@/lib/api";
import { getCsrfToken } from "@/lib/csrf";

/**
 * The signed-in shell.
 *
 * An async server component on purpose: the session and the user are resolved
 * before anything renders, so there is no flash of empty chrome and no
 * client-side redirect for an unauthenticated visitor.
 */
export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const session = await auth();

  // `error` is set when a token renewal or organization switch failed, which
  // means the stored refresh token is no longer usable.
  if (!session?.accessToken || session.error) {
    redirect("/login");
  }

  // One call establishes the user, the active organization, every organization
  // they can switch into, their effective permissions and the server's feature
  // flags -- so the shell never guesses at any of them.
  const user = await api.me();
  const csrfToken = await getCsrfToken();

  return (
    <SidebarProvider>
      <AppSidebar user={user} csrfToken={csrfToken} />
      <SidebarInset>
        <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 h-4" />
          <span className="text-sm font-medium">{user.organization.name}</span>
        </header>
        <main className="flex flex-1 flex-col gap-4 p-4">{children}</main>
      </SidebarInset>
    </SidebarProvider>
  );
}
