using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ImmersiveTB.Contracts.Services;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Services.Taskbar;
using ImmersiveTB.Helpers;
using ImmersiveTB.Logging;
using ImmersiveTB.Models;
using ImmersiveTB.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable S2325 // WinUI bindings require these instance properties.

namespace ImmersiveTB.ViewModels;

/// <summary>
///     Exposes tray-menu state and reacts to taskbar service changes.
/// </summary>
public sealed partial class TrayIconViewModel : ObservableObject, IDisposable
{
    private readonly IDispatcherService _dispatcher;
    private readonly ApplicationPreferencesService _preferences;
    private readonly ITaskbarManagerService _taskbarManager;
    private readonly ITaskbarStateService _taskbarState;
    private readonly IApplicationSettings _settings;
    private readonly ILogger<TrayIconViewModel> _logger;
    private readonly IDisposable? _optionsSubscription;
    private bool _disposed;

    /// <summary>
    ///     Connects menu commands to their existing application dependencies and observes state changes.
    /// </summary>
    public TrayIconViewModel(
        DynamicTaskbarService dynamicTaskbar,
        ApplicationPreferencesService preferences,
        ITaskbarManagerService taskbarManager,
        ITaskbarStateService taskbarState,
        IDispatcherService dispatcher,
        IApplicationSettings settings,
        StartupManager startupManager,
        IOptionsMonitor<ApplicationPreferences> options,
        ILogger<TrayIconViewModel> logger
    )
    {
        DynamicTaskbar = dynamicTaskbar;
        _preferences = preferences;
        _taskbarManager = taskbarManager;
        _taskbarState = taskbarState;
        _dispatcher = dispatcher;
        _settings = settings;
        StartupManager = startupManager;
        _logger = logger;
        _optionsSubscription = options.OnChange(_ => NotifyOnDispatcher(NotifyLogLevelProperties));
        _taskbarManager.AvailabilityChanged += OnTtbAvailabilityChanged;
        _taskbarState.StateChanged += OnTaskbarStateChanged;
    }

    /// <summary>Gets the shared dynamic taskbar controls.</summary>
    public DynamicTaskbarService DynamicTaskbar
    {
        get;
    }

    /// <summary>Gets the localized tray icon tooltip.</summary>
    public string ToolTipText => "AppDisplayName".GetLocalized();

    /// <summary>Gets the application's startup registration state.</summary>
    public StartupManager StartupManager { get; }

    /// <summary>Toggles startup registration when Windows permits it.</summary>
    [RelayCommand]
    private Task ToggleStartupAsync() => StartupManager.ToggleAsync();

    /// <summary>Gets whether trace logging is selected.</summary>
    public bool IsTraceLogLevel
    {
        get => IsLogLevel(ApplicationLogLevel.Trace);
        set => SelectLogLevel(ApplicationLogLevel.Trace, value);
    }

    /// <summary>Gets whether debug logging is selected.</summary>
    public bool IsDebugLogLevel
    {
        get => IsLogLevel(ApplicationLogLevel.Debug);
        set => SelectLogLevel(ApplicationLogLevel.Debug, value);
    }

    /// <summary>Gets whether information logging is selected.</summary>
    public bool IsInformationLogLevel
    {
        get => IsLogLevel(ApplicationLogLevel.Information);
        set => SelectLogLevel(ApplicationLogLevel.Information, value);
    }

    /// <summary>Gets whether warning logging is selected.</summary>
    public bool IsWarningLogLevel
    {
        get => IsLogLevel(ApplicationLogLevel.Warning);
        set => SelectLogLevel(ApplicationLogLevel.Warning, value);
    }

    /// <summary>Gets whether error logging is selected.</summary>
    public bool IsErrorLogLevel
    {
        get => IsLogLevel(ApplicationLogLevel.Error);
        set => SelectLogLevel(ApplicationLogLevel.Error, value);
    }

    /// <summary>Gets whether critical logging is selected.</summary>
    public bool IsCriticalLogLevel
    {
        get => IsLogLevel(ApplicationLogLevel.Critical);
        set => SelectLogLevel(ApplicationLogLevel.Critical, value);
    }

    /// <summary>Gets whether logging is disabled.</summary>
    public bool IsLoggingOff
    {
        get => IsLogLevel(ApplicationLogLevel.Off);
        set => SelectLogLevel(ApplicationLogLevel.Off, value);
    }

    /// <summary>Gets the localized TranslucentTB connection status.</summary>
    public string TtbConnectionText => _taskbarManager.IsAvailable
        ? "Tray_TtbConnected".GetLocalized()
        : "Tray_TtbDisconnected".GetLocalized();

    /// <summary>Gets the localized dynamic taskbar state.</summary>
    [SuppressMessage(
        "Performance",
        "CA1863:Use 'CompositeFormat'",
        Justification = "The format resource can change when the application language changes."
    )]
    public string TtbStateText => string.Format(
        CultureInfo.CurrentCulture,
        "Tray_TtbStateFormat".GetLocalized(),
        $"TaskbarState_{_taskbarState.CurrentState}".GetLocalized()
    );

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _optionsSubscription?.Dispose();
        _taskbarManager.AvailabilityChanged -= OnTtbAvailabilityChanged;
        _taskbarState.StateChanged -= OnTaskbarStateChanged;
    }

    /// <summary>Refreshes connection status when TranslucentTB availability changes.</summary>
    private void OnTtbAvailabilityChanged(
        object? sender,
        TaskbarAvailabilityChangedEventArgs args
    ) => NotifyOnDispatcher(() => OnPropertyChanged(nameof(TtbConnectionText)));

    /// <summary>Refreshes the displayed taskbar state on the UI thread.</summary>
    private void OnTaskbarStateChanged(
        object? sender,
        TaskbarStateChangedEventArgs args
    ) => NotifyOnDispatcher(() => OnPropertyChanged(nameof(TtbStateText)));

    /// <summary>Dispatches notifications and ignores queued updates after disposal.</summary>
    private void NotifyOnDispatcher(Action notify) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                notify();
            }
        });

    /// <summary>Includes pending edits when presenting the selected log level.</summary>
    private bool IsLogLevel(ApplicationLogLevel level) =>
        _preferences.Current.Logging.MinimumLevel == level;

    /// <summary>Updates the selected log level without reacting to radio-item deselection.</summary>
    private void SelectLogLevel(ApplicationLogLevel level, bool selected)
    {
        if (!selected || IsLogLevel(level))
        {
            return;
        }

        _preferences.Update(current => current with
        {
            Logging = current.Logging with { MinimumLevel = level }
        });
        NotifyLogLevelProperties();
    }

    /// <summary>Notifies every radio item derived from the selected log level.</summary>
    private void NotifyLogLevelProperties()
    {
        OnPropertyChanged(nameof(IsTraceLogLevel));
        OnPropertyChanged(nameof(IsDebugLogLevel));
        OnPropertyChanged(nameof(IsInformationLogLevel));
        OnPropertyChanged(nameof(IsWarningLogLevel));
        OnPropertyChanged(nameof(IsErrorLogLevel));
        OnPropertyChanged(nameof(IsCriticalLogLevel));
        OnPropertyChanged(nameof(IsLoggingOff));
    }

    /// <summary>Opens the latest log file, or the log directory before any file exists.</summary>
    [RelayCommand]
    private void OpenLogFile()
    {
        var directory = Path.Combine(_settings.LocalFolderPath, "Logs");
        Directory.CreateDirectory(directory);
        var file = Directory.EnumerateFiles(directory, "log-*.txt").MaxBy(File.GetLastWriteTimeUtc);
        OpenPath(file ?? directory);
    }

    /// <summary>Opens the application's local data directory.</summary>
    [RelayCommand]
    private void OpenData() => OpenPath(_settings.LocalFolderPath);

    /// <summary>Flushes pending edits before opening the user settings file.</summary>
    [RelayCommand]
    private void EditSettings()
    {
        _preferences.Flush();
        OpenPath(_settings.PreferencesFilePath);
    }

    /// <summary>Writes the current taskbar state to the application log.</summary>
    [RelayCommand]
    private void DumpState() => AppLogMessages.DynamicStateDumped(
        _logger,
        _taskbarState.CurrentState,
        _taskbarState.CurrentForegroundWindowHwnd,
        _taskbarManager.IsAvailable,
        _taskbarManager.CurrentColor.ToArgb(),
        !DynamicTaskbar.IsEnabled,
        DynamicTaskbar.IsDynamicThemeEnabled);

    /// <summary>Rediscovers the TranslucentTB integration targets.</summary>
    [RelayCommand]
    private void ReconnectTtb() => _taskbarManager.RefreshTargets();

    /// <summary>Refreshes taskbar state and integration targets.</summary>
    [RelayCommand]
    private void ResetState()
    {
        _taskbarState.Refresh();
        _taskbarManager.RefreshTargets();
    }

    /// <summary>Opens an application-owned path with its registered shell handler.</summary>
    private static void OpenPath(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
