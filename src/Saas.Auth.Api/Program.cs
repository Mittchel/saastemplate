using System.Text.Json.Serialization;
using Saas.Core.Middleware;
using Saas.Auth.Api;
using Saas.Auth.Api.Extensions;
using Saas.Business.Extensions;
using Saas.Core.Extensions;
using Saas.Core.Logging;
using Saas.Data.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ConfigureSerilog(builder.Configuration);

builder.Services
    .AddData(builder.Configuration)
    .AddBusiness(builder.Configuration)
    .AddSaasIdentity(builder.Configuration)
    .AddOpenIddictServer(builder.Configuration, builder.Environment);

builder.Services
    .AddControllers()
    .AddJsonOptions(json =>
        json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSaasRateLimiting();
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Shared with the resource API so both services report failures identically,
// which keeps the frontend's single error handler honest.
// Before the rate limiter, so the limiter partitions on the real client IP
// rather than on the proxy address.
app.UseForwardedProxyHeaders();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Policies are declared per controller with [EnableRateLimiting] rather than
// blanket-applied here: RequireRateLimiting on the endpoint builder takes
// precedence over the attribute, which silently disabled the stricter limit on
// registration and password-reset requests.
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
