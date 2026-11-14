"use client";

import { CheckIcon, ChevronsUpDownIcon, GalleryVerticalEndIcon, PlusIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useSession } from "next-auth/react";
import * as React from "react";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SidebarMenu, SidebarMenuButton, SidebarMenuItem } from "@/components/ui/sidebar";
import type { OrganizationSummary } from "@/lib/api";

/**
 * Replaces sidebar-07's TeamSwitcher, which shipped with a hard-coded `teams`
 * array and local state.
 *
 * Switching is not client state: the active organization is a claim in the
 * access token, so selecting one asks the identity service for a new token (via
 * the Auth.js session update, which re-checks membership server-side). Nothing
 * here could grant access on its own.
 */
export function OrgSwitcher({
  organizations,
  activeOrganizationId,
}: {
  organizations: OrganizationSummary[];
  activeOrganizationId: string;
}) {
  const router = useRouter();
  const { update } = useSession();
  const [isSwitching, startTransition] = React.useTransition();

  const active =
    organizations.find((organization) => organization.id === activeOrganizationId) ??
    organizations[0];

  function switchTo(organizationId: string) {
    if (organizationId === activeOrganizationId) {
      return;
    }

    startTransition(async () => {
      // Triggers the jwt callback, which exchanges the refresh token for one
      // scoped to the requested organization.
      await update({ organizationId });
      // Server components hold the previous tenant's data until refreshed.
      router.refresh();
    });
  }

  if (!active) {
    return null;
  }

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<SidebarMenuButton size="lg" className="aria-expanded:bg-muted" />}
            disabled={isSwitching}
          >
            <div className="bg-sidebar-primary text-sidebar-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg">
              <GalleryVerticalEndIcon className="size-4" />
            </div>
            <div className="grid flex-1 text-left text-sm leading-tight">
              <span className="truncate font-medium">{active.name}</span>
              <span className="truncate text-xs text-muted-foreground">
                {active.isPersonal ? "Personal workspace" : active.role}
              </span>
            </div>
            <ChevronsUpDownIcon className="ml-auto size-4" />
          </DropdownMenuTrigger>

          <DropdownMenuContent className="w-64" align="start" side="bottom" sideOffset={4}>
            {/*
              DropdownMenuLabel is Base UI's Menu.GroupLabel and must live
              inside a group, or the menu throws on open. Grouping the heading
              with the workspaces it labels is also what it is for.
            */}
            <DropdownMenuGroup>
              <DropdownMenuLabel className="text-muted-foreground text-xs">
                Workspaces
              </DropdownMenuLabel>

              {organizations.map((organization) => (
                <DropdownMenuItem
                  key={organization.id}
                  onClick={() => switchTo(organization.id)}
                  className="cursor-pointer gap-2"
                >
                  <span className="flex-1 truncate">{organization.name}</span>
                  {organization.id === activeOrganizationId ? (
                    <CheckIcon className="size-4" />
                  ) : null}
                </DropdownMenuItem>
              ))}
            </DropdownMenuGroup>

            <DropdownMenuSeparator />

            <DropdownMenuItem
              onClick={() => router.push("/settings/organizations")}
              className="cursor-pointer gap-2"
            >
              <PlusIcon className="size-4" />
              New workspace
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
