namespace Saas.Data.Models;

/// <summary>A pending invitation for an email address to join an organization.</summary>
public class InvitationEntity : BaseEntity, ITenantScoped
{
    public Guid OrganizationId { get; set; }

    public OrganizationEntity Organization { get; set; } = null!;

    public string Email { get; set; } = string.Empty;

    public OrganizationRole Role { get; set; } = OrganizationRole.Employee;

    /// <summary>Opaque single-use token sent in the invitation link.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public Guid InvitedByUserId { get; set; }

    public bool IsPending => AcceptedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
