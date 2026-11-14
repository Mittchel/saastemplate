using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Saas.Core.ErrorScenarios;
using Saas.Core.Exceptions;

namespace Saas.Core.Middleware;

/// <summary>
/// Turns expected failures into structured responses and everything else into a
/// generic 500.
/// </summary>
/// <remarks>
/// The response carries a machine-readable <c>scenario</c> so the frontend can
/// branch on the reason rather than parsing prose; the enum is mirrored in
/// <c>Saas-Web/lib/error-scenarios.ts</c>. Unexpected exceptions deliberately
/// return nothing but a correlation id -- leaking exception text to callers is
/// how stack traces end up in bug reports from customers.
/// </remarks>
public class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (FunctionalException exception)
        {
            logger.LogInformation(
                "Handled {Scenario}: {Message}", exception.Scenario, exception.Message);

            await WriteAsync(context, exception.StatusCode, new ErrorResponse(
                [exception.Message],
                exception.Scenario,
                context.TraceIdentifier));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteAsync(context, HttpStatusCode.InternalServerError, new ErrorResponse(
                ["An unexpected error occurred."],
                ErrorScenario.None,
                context.TraceIdentifier));
        }
    }

    private static async Task WriteAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        ErrorResponse body)
    {
        // Something further down may already have started the response, in
        // which case the status code is locked in and writing again corrupts it.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(body, SerializerOptions));
    }

    private record ErrorResponse(
        IReadOnlyList<string> Errors,
        ErrorScenario Scenario,
        string TraceId);
}
