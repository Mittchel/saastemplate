namespace Saas.Core.Constants;

/// <summary>Authorization policies that are not tied to an organization.</summary>
public static class PlatformPolicies
{
    /// <summary>Requires the <c>is_platform_admin</c> claim.</summary>
    public const string Administrator = "platform.administrator";
}
