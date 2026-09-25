using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Core.Logging;

internal static partial class CoreLogMessages
{
    [LoggerMessage(1000, LogLevel.Information, "{ServiceName} starting")]
    public static partial void ServiceStarting(ILogger logger, string serviceName);

    [LoggerMessage(1001, LogLevel.Information, "{ServiceName} started")]
    public static partial void ServiceStarted(ILogger logger, string serviceName);

    [LoggerMessage(1002, LogLevel.Information, "{ServiceName} stopped")]
    public static partial void ServiceStopped(ILogger logger, string serviceName);

    [LoggerMessage(1100, LogLevel.Debug, "Hook thread started with thread ID {ThreadId}")]
    public static partial void HookThreadStarted(ILogger logger, uint threadId);

    [LoggerMessage(1101, LogLevel.Error, "Taskbar hook thread failed")]
    public static partial void HookThreadFailed(ILogger logger, Exception exception);

    [LoggerMessage(1102, LogLevel.Debug, "Initial power saver state is {PowerSaver}")]
    public static partial void InitialPowerState(ILogger logger, bool powerSaver);

    [LoggerMessage(1103, LogLevel.Warning, "Unable to get the system power status")]
    public static partial void PowerStatusUnavailable(ILogger logger);

    [LoggerMessage(1104, LogLevel.Warning, "Unable to register the message window class; Win32 error {ErrorCode}")]
    public static partial void MessageWindowRegistrationFailed(ILogger logger, int errorCode);

    [LoggerMessage(1105, LogLevel.Warning, "Unable to create the taskbar message window")]
    public static partial void MessageWindowCreationFailed(ILogger logger);

    [LoggerMessage(1106, LogLevel.Warning, "Unable to register power notifications")]
    public static partial void PowerNotificationRegistrationFailed(ILogger logger);

    [LoggerMessage(1107, LogLevel.Debug, "Power saver state changed to {PowerSaver}")]
    public static partial void PowerStateChanged(ILogger logger, bool powerSaver);

    [LoggerMessage(1108, LogLevel.Debug, "Configured {HookCount} WinEvent hooks")]
    public static partial void EventHooksConfigured(ILogger logger, int hookCount);

    [LoggerMessage(1109, LogLevel.Warning, "Unable to create WinEvent hook for events {EventMin}-{EventMax}")]
    public static partial void EventHookCreationFailed(ILogger logger, uint eventMin, uint eventMax);

    [LoggerMessage(1110, LogLevel.Debug, "Aero Peek active state changed to {IsActive}")]
    public static partial void AeroPeekChanged(ILogger logger, bool isActive);

    [LoggerMessage(1111, LogLevel.Debug,
        "Foreground window changed from 0x{OldHandle:X} ({OldProcessName}) to 0x{NewHandle:X} ({NewProcessName})")]
    public static partial void ForegroundWindowChanged(
        ILogger logger,
        nint oldHandle,
        string oldProcessName,
        nint newHandle,
        string newProcessName
    );

    [LoggerMessage(1112, LogLevel.Debug, "Initialized taskbar state for {MonitorCount} monitors")]
    public static partial void WindowStateInitialized(ILogger logger, int monitorCount);

    [LoggerMessage(1113, LogLevel.Debug, "Taskbar state changed from {OldState} to {NewState}")]
    public static partial void TaskbarStateChanged(
        ILogger logger,
        TaskbarState oldState,
        TaskbarState newState
    );

    [LoggerMessage(1114, LogLevel.Information, "Taskbar hook thread exited")]
    public static partial void HookThreadExited(ILogger logger);

    [LoggerMessage(1115, LogLevel.Trace,
        "WinEvent {Source}: event 0x{EventId:X4}, window 0x{Window:X}, object {ObjectId}, child {ChildId}")]
    public static partial void WinEventReceived(
        ILogger logger,
        string source,
        uint eventId,
        nint window,
        int objectId,
        int childId
    );

    [LoggerMessage(1116, LogLevel.Trace,
        "Window evaluated: window 0x{Window:X}, process {ProcessId}, class {ClassName}, result {Classification}, reason {Reason}, foreground {IsForeground}, visible {IsVisible}, zoomed {IsZoomed}, iconic {IsIconic}, monitor 0x{Monitor:X}, style 0x{Style:X8}, extended style 0x{ExtendedStyle:X8}")]
    public static partial void WindowEvaluated(
        ILogger logger,
        nint window,
        uint processId,
        string className,
        string classification,
        string reason,
        bool isForeground,
        bool isVisible,
        bool isZoomed,
        bool isIconic,
        nint monitor,
        int style,
        int extendedStyle
    );

    [LoggerMessage(1117, LogLevel.Trace,
        "Calculating taskbar state: foreground 0x{Foreground:X}, monitors {MonitorCount}, normal {NormalCount}, maximized {MaximizedCount}, peek {PeekActive}, start 0x{StartMonitor:X}, search 0x{SearchMonitor:X}")]
    public static partial void TaskbarStateCalculating(
        ILogger logger,
        nint foreground,
        int monitorCount,
        int normalCount,
        int maximizedCount,
        bool peekActive,
        nint startMonitor,
        nint searchMonitor
    );

    [LoggerMessage(1118, LogLevel.Debug,
        "Publishing taskbar state {State} for foreground 0x{Foreground:X}; maximized {ForegroundMaximized}, state changed {StateChanged}, foreground changed {ForegroundChanged}, maximized changed {ForegroundMaximizedChanged}")]
    public static partial void TaskbarStatePublished(
        ILogger logger,
        TaskbarState state,
        nint foreground,
        bool foregroundMaximized,
        bool stateChanged,
        bool foregroundChanged,
        bool foregroundMaximizedChanged
    );

    [LoggerMessage(1119, LogLevel.Error, "Unable to process native taskbar event 0x{EventId:X4}")]
    public static partial void NativeEventProcessingFailed(
        ILogger logger,
        uint eventId,
        Exception exception
    );

    [LoggerMessage(1200, LogLevel.Information, "Color sampler started with {CaptureCount} capture adapters")]
    public static partial void ColorSamplerStarted(ILogger logger, int captureCount);

    [LoggerMessage(1201, LogLevel.Debug, "Falling back from Windows Graphics Capture to GDI on this Windows version")]
    public static partial void CaptureFallbackSelected(ILogger logger);

    [LoggerMessage(1202, LogLevel.Trace, "Sampling color with {CaptureMethod} from region ({X}, {Y}) {Width}x{Height}")]
    public static partial void ColorSamplingStarted(
        ILogger logger,
        ScreenCaptureMethod captureMethod,
        int x,
        int y,
        int width,
        int height
    );

    [LoggerMessage(1203, LogLevel.Error, "Color sampling failed with {CaptureMethod}")]
    public static partial void ColorSamplingFailed(
        ILogger logger,
        ScreenCaptureMethod captureMethod,
        Exception exception
    );

    [LoggerMessage(1204, LogLevel.Warning, "Capture with {CaptureMethod} failed; retrying with GDI")]
    public static partial void CaptureFailedOverToGdi(
        ILogger logger,
        ScreenCaptureMethod captureMethod,
        Exception exception
    );

    [LoggerMessage(1300, LogLevel.Information, "Dynamic taskbar theme enabled: {IsEnabled}")]
    public static partial void DynamicThemeChanged(ILogger logger, bool isEnabled);

    [LoggerMessage(1301, LogLevel.Information, "Taskbar appearance paused: {IsPaused}")]
    public static partial void PauseStateChanged(ILogger logger, bool isPaused);

    [LoggerMessage(1302, LogLevel.Debug,
        "Applying appearance for taskbar state {State} and foreground window 0x{Window:X}")]
    public static partial void ApplyingTaskbarAppearance(
        ILogger logger,
        TaskbarState state,
        nint window
    );

    [LoggerMessage(1303, LogLevel.Debug, "Taskbar appearance update was superseded")]
    public static partial void AppearanceUpdateCanceled(ILogger logger);

    [LoggerMessage(1304, LogLevel.Error, "Unable to apply the taskbar appearance for state {State}")]
    public static partial void AppearanceUpdateFailed(
        ILogger logger,
        TaskbarState state,
        Exception exception
    );

    [LoggerMessage(1400, LogLevel.Information,
        "TranslucentTB worker found: {WorkerFound}; taskbar found: {TaskbarFound}")]
    public static partial void TaskbarTargetsDiscovered(
        ILogger logger,
        bool workerFound,
        bool taskbarFound
    );

    [LoggerMessage(1401, LogLevel.Warning, "TranslucentTB worker is unavailable; color updates will be ignored")]
    public static partial void TranslucentTbUnavailable(ILogger logger);

    [LoggerMessage(1402, LogLevel.Debug,
        "Animating taskbar color to ARGB 0x{Argb:X8} over {DurationMs} ms using {Easing}")]
    public static partial void ColorAnimationStarted(
        ILogger logger,
        int argb,
        double durationMs,
        string easing
    );

    [LoggerMessage(1403, LogLevel.Debug, "Taskbar color animation was canceled")]
    public static partial void ColorAnimationCanceled(ILogger logger);

    [LoggerMessage(1404, LogLevel.Warning, "Unable to send a color update to TranslucentTB for state {State}")]
    public static partial void TranslucentTbMessageFailed(ILogger logger, TaskbarState state);

    [LoggerMessage(1500, LogLevel.Information, "System theme changed to {Theme}")]
    public static partial void SystemThemeChanged(ILogger logger, SystemTheme theme);

    [LoggerMessage(1501, LogLevel.Warning, "The Windows theme registry key is unavailable")]
    public static partial void ThemeRegistryUnavailable(ILogger logger);

    [LoggerMessage(1502, LogLevel.Warning, "Broadcasting the Windows theme change timed out or failed")]
    public static partial void ThemeBroadcastFailed(ILogger logger);

    [LoggerMessage(1503, LogLevel.Information, "System theme restored to its previous value")]
    public static partial void SystemThemeRestored(ILogger logger);

    [LoggerMessage(1504, LogLevel.Error, "Unable to restore the previous system theme")]
    public static partial void SystemThemeRestoreFailed(ILogger logger, Exception exception);
}
