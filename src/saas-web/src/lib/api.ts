/**
 * Server-side client for the .NET API.
 *
 * Every call carries the signed-in user's access token, which is what makes the
 * organization in that token decide what comes back -- the server never passes
 * a tenant id as a parameter, so a bug here cannot widen anyone's access.
 */

import { auth } from "@/auth";
import { apiUrlInternal } from "@/lib/config";
import type { ApiErrorBody } from "@/lib/error-scenarios";

export class ApiRequestError extends Error {
  constructor(
    readonly status: number,
    readonly body: ApiErrorBody | null,
    message: string,
  ) {
    super(message);
  }
}

export interface FeatureFlags {
  organizations: boolean;
  publicSignup: boolean;
}

export interface OrganizationSummary {
  id: string;
  name: string;
  slug: string;
  isPersonal: boolean;
  role: "Member" | "Admin" | "Owner";
}

export interface PlatformOrganization {
  id: string;
  name: string;
  slug: string;
  isPersonal: boolean;
  createdDate: string;
  memberCount: number;
  runCount: number;
}

export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  firstName: string;
  lastName: string;
  avatar: string | null;
  organization: OrganizationSummary;
  organizationRole: "Member" | "Admin" | "Owner";
  organizations: OrganizationSummary[];
  permissions: string[];
  /** Operates the platform: can enter any workspace. */
  isPlatformAdmin: boolean;
  features: FeatureFlags;
}

export interface MemberSummary {
  userId: string;
  email: string;
  fullName: string;
  avatar: string | null;
  role: "Member" | "Admin" | "Owner";
  joinedAt: string;
}

export interface InvitationSummary {
  id: string;
  email: string;
  role: "Member" | "Admin" | "Owner";
  expiresAt: string;
  acceptedAt: string | null;
  isPending: boolean;
}

export interface RunResponse {
  id: string;
  threadId: string;
  status: "Pending" | "Running" | "Completed" | "Failed" | "Canceled";
  model: string;
  createdDate: string;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const session = await auth();

  if (!session?.accessToken) {
    throw new ApiRequestError(401, null, "Not signed in.");
  }

  const response = await fetch(`${apiUrlInternal()}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${session.accessToken}`,
      ...init.headers,
    },
    // Tenant-scoped data must never be served from a shared cache.
    cache: "no-store",
  });

  if (!response.ok) {
    let body: ApiErrorBody | null = null;
    try {
      body = (await response.json()) as ApiErrorBody;
    } catch {
      // A non-JSON error body (a gateway 502, say) is not worth failing over.
    }

    throw new ApiRequestError(
      response.status,
      body,
      body?.errors?.[0] ?? `Request to ${path} failed with ${response.status}.`,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/**
 * Reads the deployment flags without a session.
 *
 * Used by the sign-up screen, which has to know whether public signup is open
 * before anyone can authenticate.
 */
export async function fetchFeatures(): Promise<FeatureFlags> {
  const response = await fetch(`${apiUrlInternal()}/features`, { cache: "no-store" });

  if (!response.ok) {
    // Fail open on presentation only: the API still enforces the flag.
    return { organizations: true, publicSignup: true };
  }

  return (await response.json()) as FeatureFlags;
}

export const api = {
  me: () => request<CurrentUser>("/me"),

  updateProfile: (firstName: string, lastName: string) =>
    request<CurrentUser>("/me", {
      method: "PUT",
      body: JSON.stringify({ firstName, lastName }),
    }),

  organizations: () => request<OrganizationSummary[]>("/organizations"),

  createOrganization: (name: string) =>
    request<OrganizationSummary>("/organizations", {
      method: "POST",
      body: JSON.stringify({ name }),
    }),

  members: (organizationId: string) =>
    request<MemberSummary[]>(`/organizations/${organizationId}/members`),

  updateMemberRole: (organizationId: string, userId: string, role: string) =>
    request<MemberSummary>(`/organizations/${organizationId}/members/${userId}`, {
      method: "PUT",
      body: JSON.stringify({ role }),
    }),

  removeMember: (organizationId: string, userId: string) =>
    request<void>(`/organizations/${organizationId}/members/${userId}`, {
      method: "DELETE",
    }),

  invitations: (organizationId: string) =>
    request<InvitationSummary[]>(`/organizations/${organizationId}/invitations`),

  createInvitation: (organizationId: string, email: string, role: string) =>
    request<{ invitation: InvitationSummary; token: string }>(
      `/organizations/${organizationId}/invitations`,
      { method: "POST", body: JSON.stringify({ email, role }) },
    ),

  revokeInvitation: (organizationId: string, invitationId: string) =>
    request<void>(`/organizations/${organizationId}/invitations/${invitationId}`, {
      method: "DELETE",
    }),


  platformOrganizations: () => request<PlatformOrganization[]>("/platform/organizations"),

  runs: (threadId?: string) =>
    request<RunResponse[]>(threadId ? `/runs?threadId=${encodeURIComponent(threadId)}` : "/runs"),

  createRun: (input: string, threadId?: string) =>
    request<RunResponse>("/runs", {
      method: "POST",
      body: JSON.stringify({ input, threadId: threadId ?? null }),
    }),
};
