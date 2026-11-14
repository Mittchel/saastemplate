namespace Saas.Core.ErrorScenarios;

/// <summary>
/// Machine-readable reason codes returned alongside an error response.
/// </summary>
/// <remarks>
/// Serialized as a string so the frontend can switch on the name rather than a
/// magic number. Mirrored in <c>Saas-Web/lib/error-scenarios.ts</c> -- keep the
/// two in sync whenever a member is added.
/// </remarks>
public enum ErrorScenario
{
    None = 0,

    // Generic
    NotFound = 1,
    ValidationFailed = 2,
    Conflict = 3,

    // Identity
    InvalidCredentials = 100,
    EmailNotConfirmed = 101,
    EmailAlreadyRegistered = 102,
    PasswordTooWeak = 103,

    // Organizations
    OrganizationNotFound = 200,
    NotAMember = 201,
    InsufficientOrganizationRole = 202,
    CannotLeavePersonalOrganization = 203,
    LastOwnerCannotBeRemoved = 204,
    InviteExpired = 205,
    InviteAlreadyAccepted = 206,
    OrganizationsDisabled = 207,


    // AI runs
    RunNotFound = 400,
    RunAlreadyCompleted = 401,
}
