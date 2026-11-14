namespace Saas.Core.Options;

/// <summary>Where the application lives, from the outside.</summary>
/// <remarks>
/// The backend has to build links into the frontend -- password resets,
/// invitations -- and cannot infer the public URL from an internal request.
/// </remarks>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public base URL of the web app, with no trailing slash.</summary>
    public string WebUrl { get; set; } = "http://app.localhost";
}
