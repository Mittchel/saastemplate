// Translation comes later; until then the role names are shown in English.
const ROLE_LABELS: Record<string, string> = {
  Employee: "Employee",
  Manager: "Manager",
  HrAdmin: "HR admin",
};

export function roleLabel(role: string): string {
  return ROLE_LABELS[role] ?? role;
}
