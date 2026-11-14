using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Saas.Core.Logging;

/// <summary>
/// Wires Serilog from configuration, keeping the established convention of a
/// single <c>ConfigureSerilog</c> call in each service's startup.
/// </summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Replaces the default providers with Serilog, reading the
    /// <c>Serilog</c> configuration section when present and otherwise falling
    /// back to a console sink.
    /// </summary>
    public static ILoggingBuilder ConfigureSerilog(
        this ILoggingBuilder builder,
        IConfiguration configuration)
    {
        var hasConfigSection = configuration.GetSection("Serilog").Exists();

        var loggerConfiguration = new LoggerConfiguration();

        if (hasConfigSection)
        {
            loggerConfiguration.ReadFrom.Configuration(configuration);
        }
        else
        {
            // Containers capture stdout, so console-only is the right default;
            // file sinks are opt-in through appsettings.
            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:dd-MM-yyyy HH:mm:ss.ffff} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        }

        loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId();

        Log.Logger = loggerConfiguration.CreateLogger();

        builder.ClearProviders();
        builder.AddSerilog(Log.Logger, dispose: true);

        return builder;
    }
}
