using System.Text.Json.Serialization;
using Saas.Api;
using Saas.Api.Extensions;
using Saas.Core.Middleware;
using Saas.Business.Extensions;
using Saas.Core.Extensions;
using Saas.Core.Logging;
using Saas.Data.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ConfigureSerilog(builder.Configuration);

builder.Services
    .AddData(builder.Configuration)
    .AddBusiness(builder.Configuration)
    .AddTenancy()
    .AddBearerAuthentication(builder.Configuration)
    .AddPermissionPolicies()
    .AddSwaggerGenerator()
    .AddWebCors(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(json =>
    {
        // Enums travel as names so the frontend can switch on them; the
        // ErrorScenario enum is mirrored in Saas-Web/lib/error-scenarios.ts.
        json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSaasRateLimiting();
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<StaleRunReaper>();

var app = builder.Build();

// First in the pipeline, so it also catches failures thrown by the
// authentication and authorization middleware below.
app.UseForwardedProxyHeaders();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting(RateLimitingExtensions.DefaultPolicy);
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
