using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace PeekVPN.Core.Logging;

/// <summary>
/// Central Serilog policy used by both executable composition roots.
/// </summary>
public static class PeekVpnLogging
{
    public const int RetainedFileCountLimit = 14;

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PeekVPN",
        "logs");

    public static Serilog.ILogger CreateLogger()
    {
        Directory.CreateDirectory(LogDirectory);

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Logger(application => application
                .Filter.ByIncludingOnly(Matching.FromSource("PeekVPN.App"))
                .WriteTo.File(
                    Path.Combine(LogDirectory, "application-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: RetainedFileCountLimit,
                    outputTemplate: OutputTemplate))
            .WriteTo.Logger(service => service
                .Filter.ByIncludingOnly(IsServiceEvent)
                .WriteTo.File(
                    Path.Combine(LogDirectory, "service-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: RetainedFileCountLimit,
                    outputTemplate: OutputTemplate));

#if DEBUG
        configuration.WriteTo.Console(outputTemplate: OutputTemplate);
#endif

        return configuration.CreateLogger();
    }

    public static IServiceCollection AddPeekVpnLogging(this IServiceCollection services, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        services.AddLogging(logging => logging
            .ClearProviders()
            .AddSerilog(logger, dispose: false));

        return services;
    }

    private static bool IsServiceEvent(LogEvent logEvent)
    {
        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            return false;
        }

        var source = sourceContext.ToString().Trim('"');
        return source.StartsWith("PeekVPN.Core", StringComparison.Ordinal)
            || source.StartsWith("PeekVPN.Infrastructure", StringComparison.Ordinal)
            || source.StartsWith("PeekVPN.Service", StringComparison.Ordinal);
    }
}
