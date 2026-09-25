using Windows.Graphics.Capture;
using Windows.Security.Authorization.AppCapabilityAccess;
using ImmersiveTB.Core.Models;
using ImmersiveTB.Models;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Logging;

internal static partial class AppLogMessages
{
    [LoggerMessage(2000, LogLevel.Information, "ImmersiveTB starting")]
    public static partial void ApplicationStarting(ILogger logger);

    [LoggerMessage(2001, LogLevel.Information, "ImmersiveTB started")]
    public static partial void ApplicationStarted(ILogger logger);

    [LoggerMessage(2002, LogLevel.Information, "ImmersiveTB shutting down")]
    public static partial void ApplicationStopping(ILogger logger);

    [LoggerMessage(2003, LogLevel.Critical, "Unhandled application exception")]
    public static partial void UnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(2004, LogLevel.Debug, "Tray icon initialized")]
    public static partial void TrayIconInitialized(ILogger logger);

    [LoggerMessage(2005, LogLevel.Debug, "Main window created")]
    public static partial void MainWindowCreated(ILogger logger);

    [LoggerMessage(2006, LogLevel.Debug, "Main window activated")]
    public static partial void MainWindowActivated(ILogger logger);

    [LoggerMessage(2007, LogLevel.Error, "Unable to dispatch main-window activation to the UI thread")]
    public static partial void MainWindowDispatchFailed(ILogger logger);

    [LoggerMessage(2008, LogLevel.Information, "Application theme applied: {Theme}")]
    public static partial void ApplicationThemeApplied(ILogger logger, AppTheme theme);

    [LoggerMessage(2009, LogLevel.Error, "Unable to create or activate the main window")]
    public static partial void MainWindowActivationFailed(ILogger logger, Exception exception);

    [LoggerMessage(2010, LogLevel.Error, "Unable to access the startup task")]
    public static partial void StartupTaskOperationFailed(ILogger logger, Exception exception);

    [LoggerMessage(2011, LogLevel.Information,
        "Dynamic state dump: taskbar state {State}, foreground 0x{ForegroundWindow:X}, TranslucentTB available {TtbAvailable}, color ARGB 0x{Argb:X8}, paused {IsPaused}, dynamic theme {DynamicThemeEnabled}")]
    public static partial void DynamicStateDumped(
        ILogger logger,
        TaskbarState state,
        nint foregroundWindow,
        bool ttbAvailable,
        int argb,
        bool isPaused,
        bool dynamicThemeEnabled
    );

    [LoggerMessage(2100, LogLevel.Information, "App notifications registered")]
    public static partial void NotificationsRegistered(ILogger logger);

    [LoggerMessage(2101, LogLevel.Information, "App notifications unregistered")]
    public static partial void NotificationsUnregistered(ILogger logger);

    [LoggerMessage(2102, LogLevel.Debug, "App notification invoked")]
    public static partial void NotificationInvoked(ILogger logger);

    [LoggerMessage(2200, LogLevel.Debug, "Application theme initialized to {Theme}")]
    public static partial void ThemeInitialized(ILogger logger, AppTheme theme);

    [LoggerMessage(2201, LogLevel.Information, "Application theme changed to {Theme}")]
    public static partial void ThemeChanged(ILogger logger, AppTheme theme);

    [LoggerMessage(2300, LogLevel.Debug, "Windows Graphics Capture {AccessKind} access status: {AccessStatus}")]
    public static partial void GraphicsCaptureAccessResolved(
        ILogger logger,
        GraphicsCaptureAccessKind accessKind,
        AppCapabilityAccessStatus accessStatus
    );

    [LoggerMessage(2400, LogLevel.Debug, "Settings loaded")]
    public static partial void SettingsLoaded(ILogger logger);

    [LoggerMessage(2401, LogLevel.Debug, "Setting {SettingKey} persisted")]
    public static partial void SettingPersisted(ILogger logger, string settingKey);

    [LoggerMessage(2402, LogLevel.Debug, "Package identity is unavailable; using the assembly version")]
    public static partial void AssemblyVersionFallback(ILogger logger);

    /// <summary>Reports a failed write while retaining pending settings for retry.</summary>
    [LoggerMessage(2403, LogLevel.Error, "Unable to save settings; changes remain pending")]
    public static partial void SettingsSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(2500, LogLevel.Debug, "Navigated to {PageName}")]
    public static partial void Navigated(ILogger logger, string pageName);

    [LoggerMessage(2501, LogLevel.Debug, "Title bar content width: {TitleWidth}")]
    public static partial void TitleBarMeasured(ILogger logger, double titleWidth);
}