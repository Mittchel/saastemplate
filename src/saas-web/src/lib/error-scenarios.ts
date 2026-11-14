/**
 * Mirror of `Saas.Core.ErrorScenarios.ErrorScenario`.
 *
 * Both .NET services serialize the enum by name and return it as `scenario` on
 * every error response, so the UI can branch on the reason instead of matching
 * prose. Keep this list in sync when a member is added on the server.
 */
export type ErrorScenario =
  | "None"
  | "NotFound"
  | "ValidationFailed"
  | "Conflict"
  | "InvalidCredentials"
  | "EmailNotConfirmed"
  | "EmailAlreadyRegistered"
  | "PasswordTooWeak"
  | "OrganizationNotFound"
  | "NotAMember"
  | "InsufficientOrganizationRole"
  | "CannotLeavePersonalOrganization"
  | "LastOwnerCannotBeRemoved"
  | "InviteExpired"
  | "InviteAlreadyAccepted"
  | "OrganizationsDisabled"
  | "RunNotFound"
  | "RunAlreadyCompleted";

export interface ApiErrorBody {
  errors: string[];
  scenario: ErrorScenario;
  traceId: string;
}

/** Messages worth replacing with something friendlier than the server's wording. */
const FRIENDLY: Partial<Record<ErrorScenario, string>> = {
  InvalidCredentials: "That email address or password is incorrect.",
  EmailAlreadyRegistered: "An account with this email address already exists.",
  LastOwnerCannotBeRemoved:
    "An organization needs at least one owner. Promote someone else first.",
  CannotLeavePersonalOrganization: "You cannot leave your personal workspace.",
  InviteExpired: "This invitation has expired. Ask for a new one.",
  OrganizationsDisabled: "Organizations are turned off for this deployment.",
};

export function describeError(body: ApiErrorBody | null, fallback: string): string {
  if (!body) {
    return fallback;
  }

  return FRIENDLY[body.scenario] ?? body.errors[0] ?? fallback;
}
