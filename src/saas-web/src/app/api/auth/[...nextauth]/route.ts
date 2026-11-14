import { handlers } from "@/auth";

// Also serves POST /api/auth/signout, which the sidebar user menu submits to as
// a plain form so signing out works without JavaScript.
export const { GET, POST } = handlers;
