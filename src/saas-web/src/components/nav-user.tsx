"use client";

import { ChevronsUpDownIcon, LogOutIcon, UserIcon } from "lucide-react";
import Link from "next/link";

import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";

/** Two-letter fallback, derived from the email so it is always available. */
function initials(email: string, name: string): string {
  const source = name.trim() || email;
  const parts = source.split(/[\s@._-]+/).filter(Boolean);

  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }

  return source.slice(0, 2).toUpperCase();
}

export function NavUser({
  user,
  csrfToken,
}: {
  user: { name: string; email: string; avatar: string | null };
  /** Required by Auth.js; without it the sign-out POST is rejected. */
  csrfToken: string | null;
}) {
  const { isMobile } = useSidebar();

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<SidebarMenuButton size="lg" className="aria-expanded:bg-muted" />}
          >
            <Avatar className="size-8 rounded-lg">
              {user.avatar ? <AvatarImage src={user.avatar} alt={user.name} /> : null}
              <AvatarFallback className="rounded-lg">
                {initials(user.email, user.name)}
              </AvatarFallback>
            </Avatar>
            <div className="grid flex-1 text-left text-sm leading-tight">
              <span className="truncate font-medium">{user.name || user.email}</span>
              <span className="truncate text-xs text-muted-foreground">{user.email}</span>
            </div>
            <ChevronsUpDownIcon className="ml-auto size-4" />
          </DropdownMenuTrigger>

          <DropdownMenuContent
            className="w-56"
            // Opens beside the rail on desktop, above the bar on mobile.
            side={isMobile ? "bottom" : "right"}
            align="end"
            sideOffset={4}
          >
            {/*
              The label must sit inside a group. DropdownMenuLabel is Base UI's
              Menu.GroupLabel, which throws "MenuGroupContext is missing" when
              rendered outside Menu.Group -- the menu then crashes the moment it
              opens. Radix had no such requirement, so a label lifted from a
              Radix-era example looks correct and fails at runtime.
            */}
            <DropdownMenuGroup>
              <DropdownMenuLabel className="font-normal">
                <div className="flex flex-col gap-0.5">
                  <span className="truncate text-sm font-medium">{user.name || user.email}</span>
                  <span className="truncate text-xs text-muted-foreground">{user.email}</span>
                </div>
              </DropdownMenuLabel>
            </DropdownMenuGroup>

            <DropdownMenuSeparator />

            <DropdownMenuItem render={<Link href="/settings/account" />}>
              <UserIcon className="size-4" />
              Account
            </DropdownMenuItem>

            <DropdownMenuSeparator />

            {/*
              A real form POST to Auth.js's own endpoint rather than a click
              handler: signing out then works even if the page's JavaScript
              failed to load, and it cannot be triggered cross-site because
              Auth.js checks its CSRF token.
            */}
            <form action="/api/auth/signout" method="POST">
              {/*
                Auth.js refuses a sign-out POST with no CSRF token and bounces
                to /login?error=MissingCSRF instead of signing out. Its client
                helpers add this automatically; a plain form must carry it.
              */}
              <input type="hidden" name="csrfToken" value={csrfToken ?? ""} />
              <DropdownMenuItem
                render={<button type="submit" className="w-full cursor-pointer" />}
              >
                <LogOutIcon className="size-4" />
                Sign out
              </DropdownMenuItem>
            </form>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
