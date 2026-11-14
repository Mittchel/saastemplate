namespace Saas.Api;

/// <summary>
/// Marker type naming this assembly for <c>WebApplicationFactory</c>.
/// </summary>
/// <remarks>
/// The earlier templates exposed <c>public partial class Program</c> for this.
/// That does not work here: top-level statements emit <c>Program</c> into the
/// global namespace, and the test project references both web services, so two
/// such declarations collide. <c>WebApplicationFactory&lt;T&gt;</c> only uses
/// its type argument to locate the entry assembly, so any public type serves --
/// and a named marker says so out loud.
/// </remarks>
public sealed class ApiEntryPoint;
