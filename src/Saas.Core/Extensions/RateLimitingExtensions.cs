using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Saas.Core.Extensions;

/// <summary>
/// Per-client request limits.
/// </summary>
/// <remarks>
/// Partitioned by client IP rather than by account. ASP.NET Identity already
/// locks an account after repeated failures, which stops an attacker grinding
/// one password -- but it does nothing against the same attacker trying one
/// common password against thousands of accounts, and it hands anyone a way to
/// lock a victim out on purpose. A per-IP limit covers what lockout cannot.
/// </remarks>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Limit for the identity service.
    /// </summary>
    /// <remarks>
    /// Generous enough for real use -- the token endpoint also serves refreshes
    /// and organization switches, and an app with a few tabs open hits it
    /// repeatedly in a burst -- while still capping a password-guessing client
    /// far below anything useful. Set too low it throttles ordinary sign-ins,
    /// which is how the first version of this broke the test suite.
    /// </remarks>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Tight limit for genuinely infrequent, abusable endpoints: registration
    /// and password-reset requests, which send mail to an address the caller
    /// chooses.
    /// </summary>
    public const string SensitivePolicy = "sensitive";

    /// <summary>Ordinary limit for everything else.</summary>
    public const string DefaultPolicy = "default";

    public static IServiceCollection AddSaasRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.OnRejected = async (context, token) =>
            {
                // Tell the caller when to come back; a client that retries
                // blindly otherwise turns a limit into a hot loop.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/json";

                await context.HttpContext.Response.WriteAsync(
                    """{"errors":["Too many requests. Try again shortly."],"scenario":"None"}""",
                    token);
            };

            // Sliding rather than fixed window: a fixed window lets a caller
            // spend its whole allowance at the end of one window and again at
            // the start of the next, which is double the intended burst.
            limiter.AddPolicy(SensitivePolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    }));

            limiter.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    }));

            limiter.AddPolicy(DefaultPolicy, context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientKey(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    /// <summary>
    /// Trusts the proxy's forwarded headers.
    /// </summary>
    /// <remarks>
    /// Without this every request appears to come from Traefik, so the whole
    /// deployment shares one rate-limit bucket and a single abusive client
    /// throttles everybody. The known-proxy list is cleared because the proxy
    /// sits on a container network with an address assigned at runtime; that is
    /// safe only because nothing but the proxy can reach these services -- do
    /// not expose a service port directly while this is set.
    /// </remarks>
    public static IApplicationBuilder UseForwardedProxyHeaders(this IApplicationBuilder app)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 2,
        };

        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        return app.UseForwardedHeaders(options);
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
