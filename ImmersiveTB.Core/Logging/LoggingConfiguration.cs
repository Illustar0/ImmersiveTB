using Serilog;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace ImmersiveTB.Core.Logging;

/// <summary>
///     Configures and controls application logging.
/// </summary>
public static class LoggingConfiguration
{
    private static readonly LoggingLevelSwitch LevelSwitch = new();
    private static int _level = (int)ApplicationLogLevel.Information;

    /// <summary>
    ///     Gets the active minimum log level.
    /// </summary>
    public static ApplicationLogLevel Level =>
        (ApplicationLogLevel)Volatile.Read(ref _level);

    /// <summary>
    ///     Configures the host-owned Serilog logger.
    /// </summary>
    /// <param name="configuration">The host logger configuration.</param>
    /// <param name="logDirectory">Directory that receives rolling text logs.</param>
    /// <param name="level">Initial minimum log level.</param>
    public static void Configure(
        LoggerConfiguration configuration,
        string logDirectory,
        ApplicationLogLevel level = ApplicationLogLevel.Information
    )
    {
        SetLevel(level);
        configuration
            .MinimumLevel.ControlledBy(LevelSwitch)
            .Filter.ByExcluding(_ => Level == ApplicationLogLevel.Off)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
#if DEBUG
            .WriteTo.Debug(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [Thread: {ThreadId,-2}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            )
#endif
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [Thread: {ThreadId,-2}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture,
                theme: AnsiConsoleTheme.Code,
                applyThemeToRedirectedOutput: true
            )
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [Thread: {ThreadId,-2}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture
            );
    }

    /// <summary>
    ///     Changes the runtime minimum log level.
    /// </summary>
    /// <param name="level">New minimum level.</param>
    public static void SetLevel(ApplicationLogLevel level)
    {
        Volatile.Write(ref _level, (int)level);
        LevelSwitch.MinimumLevel = level switch
        {
            ApplicationLogLevel.Trace => LogEventLevel.Verbose,
            ApplicationLogLevel.Debug => LogEventLevel.Debug,
            ApplicationLogLevel.Information => LogEventLevel.Information,
            ApplicationLogLevel.Warning => LogEventLevel.Warning,
            ApplicationLogLevel.Error => LogEventLevel.Error,
            ApplicationLogLevel.Critical => LogEventLevel.Fatal,
            ApplicationLogLevel.Off => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}