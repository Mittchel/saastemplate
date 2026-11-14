import { redirect } from "next/navigation";

import { auth } from "@/auth";

/** Sends visitors to the app or the login screen; there is no marketing page. */
export default async function RootPage() {
  const session = await auth();

  redirect(session?.accessToken && !session.error ? "/dashboard" : "/login");
}
