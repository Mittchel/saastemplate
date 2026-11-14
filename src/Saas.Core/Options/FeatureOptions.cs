namespace Saas.Core.Options;

/// <summary>
/// Template feature switches. Surfaced to the frontend by <c>GET /me</c> so the
/// UI never hard-codes what the backend has enabled.
/// </summary>
public class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>
    /// Whether organization management is exposed.
    /// </summary>
    /// <remarks>
    /// Organizations exist in the schema regardless, and every user always has
    /// a personal organization -- this only governs whether the switcher,
    /// member management and invitations are available. Turning it on later
    /// requires no migration.
    /// </remarks>
    public bool Organizations { get; set; } = true;

    /// <summary>Whether self-service signup is open, as opposed to invite-only.</summary>
    public bool PublicSignup { get; set; } = true;
}
