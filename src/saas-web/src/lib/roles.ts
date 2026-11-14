// Documented exception to the "every string in next-intl message files" rule:
// DIA-88 states translation comes later, and next-intl is not installed yet
// (the template has no message files). This is the single place role labels
// live, so moving them into the nl/en message files later is a one-file change.
const ROLE_LABELS: Record<string, string> = {
  Employee: "Employee",
  Manager: "Manager",
  HrAdmin: "HR admin",
};

export function roleLabel(role: string): string {
  return ROLE_LABELS[role] ?? role;
}
