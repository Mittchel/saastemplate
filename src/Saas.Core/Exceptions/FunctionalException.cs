using System.Net;
using Saas.Core.ErrorScenarios;

namespace Saas.Core.Exceptions;

/// <summary>
/// An expected, user-facing failure. Thrown by services and translated into a
/// response body by the API's error-handling middleware; anything else that
/// escapes becomes a generic 500.
/// </summary>
public class FunctionalException : Exception
{
    public FunctionalException(
        string message,
        ErrorScenario scenario = ErrorScenario.None,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        Scenario = scenario;
        StatusCode = statusCode;
    }

    public ErrorScenario Scenario { get; }

    public HttpStatusCode StatusCode { get; }

    public static FunctionalException NotFound(string what) =>
        new($"{what} was not found.", ErrorScenario.NotFound, HttpStatusCode.NotFound);

    public static FunctionalException Forbidden(string message, ErrorScenario scenario) =>
        new(message, scenario, HttpStatusCode.Forbidden);

    public static FunctionalException Conflict(string message, ErrorScenario scenario) =>
        new(message, scenario, HttpStatusCode.Conflict);
}
