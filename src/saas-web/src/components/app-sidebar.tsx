"use client";

import {
  GalleryVerticalEndIcon,
  LayoutDashboardIcon,
  MailIcon,
  ShieldIcon,
  MessagesSquareIcon,
  SettingsIcon,
  UsersIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { NavUser } from "@/components/nav-user";
import { OrgSwitcher } from "@/components/org-switcher";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/ui/sidebar";
import type { CurrentUser } from "@/lib/api";

interface NavItem {
  title: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  /** Hidden unless the signed-in user holds this permission. */
  permission?: string;
  /** Hidden unless the Organizations feature is enabled. */
  requiresOrganizations?: boolean;
}

const MAIN_NAV: NavItem[] = [
  { title: "Dashboard", href: "/dashboard", icon: LayoutDashboardIcon },
  { title: "Chat", href: "/chat", icon: MessagesSquareIcon, permission: "run.create" },
];

const SETTINGS_NAV: NavItem[] = [
  { title: "Account", href: "/settings/account", icon: SettingsIcon },
  {
    title: "Members",
    href: "/settings/members",
    icon: UsersIcon,
    permission: "member.read",
    requiresOrganizations: true,
  },
  {
    title: "Invitations",
    href: "/settings/invitations",
    icon: MailIcon,
    permission: "member.invite",
    requiresOrganizations: true,
  },
];

export function AppSidebar({
  user,
  csrfToken,
  ...props
}: React.ComponentProps<typeof Sidebar> & {
  user: CurrentUser;
  csrfToken: string | null;
}) {
  const pathname = usePathname();

  // Both gates come from the server's own answer to GET /me, so the navigation
  // can never advertise something the API would refuse.
  const visible = (item: NavItem) =>
    (!item.permission || user.permissions.includes(item.permission)) &&
    (!item.requiresOrganizations || user.features.organizations);

  const mainItems = MAIN_NAV.filter(visible);
  const settingsItems = SETTINGS_NAV.filter(visible);

  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        {user.features.organizations ? (
          <OrgSwitcher
            organizations={user.organizations}
            activeOrganizationId={user.organization.id}
          />
        ) : (
          // With organizations off there is exactly one workspace, so a
          // switcher would be a control that does nothing.
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton size="lg" render={<Link href="/dashboard" />}>
                <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
                  <GalleryVerticalEndIcon className="size-4" />
                </div>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-medium">Saas</span>
                  <span className="truncate text-xs text-muted-foreground">
                    {user.organization.name}
                  </span>
                </div>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        )}
      </SidebarHeader>

      <SidebarContent>
        {/* Operators only. Gated on the server answer, not a local guess. */}
        {user.isPlatformAdmin ? (
          <SidebarGroup>
            <SidebarGroupLabel>Platform</SidebarGroupLabel>
            <SidebarMenu>
              <SidebarMenuItem>
                <SidebarMenuButton
                  tooltip="Workspaces"
                  isActive={pathname === "/platform"}
                  render={<Link href="/platform" />}
                >
                  <ShieldIcon className="size-4" />
                  <span>All workspaces</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </SidebarGroup>
        ) : null}

        <SidebarGroup>
          <SidebarGroupLabel>Workspace</SidebarGroupLabel>
          <SidebarMenu>
            {mainItems.map((item) => (
              <SidebarMenuItem key={item.href}>
                <SidebarMenuButton
                  tooltip={item.title}
                  isActive={pathname === item.href || pathname.startsWith(`${item.href}/`)}
                  render={<Link href={item.href} />}
                >
                  <item.icon className="size-4" />
                  <span>{item.title}</span>
                </SidebarMenuButton>
              </SidebarMenuItem>
            ))}
          </SidebarMenu>
        </SidebarGroup>

        {settingsItems.length > 0 ? (
          <SidebarGroup>
            <SidebarGroupLabel>Settings</SidebarGroupLabel>
            <SidebarMenu>
              {settingsItems.map((item) => (
                <SidebarMenuItem key={item.href}>
                  <SidebarMenuButton
                    tooltip={item.title}
                    isActive={pathname === item.href}
                    render={<Link href={item.href} />}
                  >
                    <item.icon className="size-4" />
                    <span>{item.title}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroup>
        ) : null}
      </SidebarContent>

      <SidebarFooter>
        <NavUser
          csrfToken={csrfToken}
          user={{
            name: user.fullName,
            email: user.email,
            avatar: user.avatar,
          }}
        />
      </SidebarFooter>

      <SidebarRail />
    </Sidebar>
  );
}
