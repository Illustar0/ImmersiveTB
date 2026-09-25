using Windows.Win32;
using Windows.Win32.Foundation;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Coordinates native observations, tracked windows, state reduction, and publication.
/// </summary>
internal sealed class TaskbarStateController(
    TaskbarWindowTracker windowTracker,
    TaskbarForegroundTracker foregroundTracker,
    IDispatcherService dispatcher,
    Action<TaskbarStateTransition> publish,
    ILogger logger
)
{
    private readonly HashSet<nint> _pendingLocationChanges = [];
    private readonly Lock _stateLock = new();
    private readonly TaskbarStateReducer _stateReducer = new();
    private bool _locationRefreshPending;
    private bool _peekActive;
    private bool _powerSaver;
    private int _refreshPending;
    private bool _taskViewActive;

    /// <summary>Gets the latest reduced taskbar state.</summary>
    public TaskbarState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _stateReducer.CurrentState;
            }
        }
    }

    /// <summary>Gets the latest observed foreground window.</summary>
    public IntPtr ForegroundWindow
    {
        get
        {
            lock (_stateLock)
            {
                return foregroundTracker.Current.Window;
            }
        }
    }

    /// <summary>
    ///     Applies one typed native observation to the tracked taskbar state.
    /// </summary>
    public void Observe(TaskbarNativeEvent observation)
    {
        var refresh = observation.Kind switch
        {
            TaskbarNativeEventKind.Initialize => Initialize(),
            TaskbarNativeEventKind.PowerSaverChanged =>
                SetPowerSaver(observation.IsActive),
            TaskbarNativeEventKind.PeekChanged => SetPeekActive(observation.IsActive),
            TaskbarNativeEventKind.WindowUpserted => UpsertWindow(observation.Window),
            TaskbarNativeEventKind.WindowLocationChanged =>
                QueueLocationChange(observation.Window),
            TaskbarNativeEventKind.WindowRemoved => RemoveWindow(observation.Window),
            TaskbarNativeEventKind.ForegroundChanged =>
                SetForegroundWindow(observation.Window),
            _ => false
        };

        if (refresh)
        {
            PostRefreshState();
        }
    }

    /// <summary>
    ///     Rebuilds the tracked window catalog and republishes the current state.
    /// </summary>
    public void RefreshWindows()
    {
        lock (_stateLock)
        {
            foregroundTracker.Refresh();
            windowTracker.Rebuild(foregroundTracker.Current.Window);
        }

        PostRefreshState();
    }

    /// <summary>
    ///     Updates the upstream-compatible task-view state input.
    /// </summary>
    public void SetTaskViewActive(bool active)
    {
        lock (_stateLock)
        {
            _taskViewActive = active;
        }

        PostRefreshState();
    }

    private bool Initialize()
    {
        lock (_stateLock)
        {
            foregroundTracker.Refresh();
            windowTracker.Rebuild(foregroundTracker.Current.Window);
        }

        return true;
    }

    private bool SetPowerSaver(bool active)
    {
        lock (_stateLock)
        {
            if (_powerSaver == active)
            {
                return false;
            }

            _powerSaver = active;
            return true;
        }
    }

    private bool SetPeekActive(bool active)
    {
        lock (_stateLock)
        {
            if (_peekActive == active)
            {
                return false;
            }

            _peekActive = active;
            return true;
        }
    }

    private bool UpsertWindow(HWND window)
    {
        lock (_stateLock)
        {
            return windowTracker.Upsert(window, foregroundTracker.Current.Window);
        }
    }

    private bool QueueLocationChange(HWND window)
    {
        lock (_stateLock)
        {
            _pendingLocationChanges.Add(window);
            if (_locationRefreshPending)
            {
                return false;
            }

            _locationRefreshPending = true;
        }

        if (!dispatcher.TryEnqueue(ApplyPendingLocationChanges))
        {
            lock (_stateLock)
            {
                _locationRefreshPending = false;
            }
        }

        return false;
    }

    private void ApplyPendingLocationChanges()
    {
        var refresh = false;
        lock (_stateLock)
        {
            foreach (var window in _pendingLocationChanges)
            {
                refresh |= windowTracker.Upsert(
                    (HWND)window,
                    foregroundTracker.Current.Window
                );
            }

            _pendingLocationChanges.Clear();
            _locationRefreshPending = false;
        }

        if (refresh)
        {
            PostRefreshState();
        }
    }

    private bool RemoveWindow(HWND window)
    {
        lock (_stateLock)
        {
            return windowTracker.Remove(window);
        }
    }

    private bool SetForegroundWindow(HWND window)
    {
        lock (_stateLock)
        {
            return foregroundTracker.Update(window, true);
        }
    }

    private void PostRefreshState()
    {
        if (Interlocked.Exchange(ref _refreshPending, 1) != 0)
        {
            return;
        }

        if (!dispatcher.TryEnqueue(RefreshPendingState))
        {
            Volatile.Write(ref _refreshPending, 0);
        }
    }

    private void RefreshPendingState()
    {
        Volatile.Write(ref _refreshPending, 0);

        TaskbarStateTransition transition;
        TaskbarWindowSnapshot windows;
        TaskbarForegroundSnapshot foreground;
        bool powerSaver;
        bool peekActive;
        lock (_stateLock)
        {
            windows = windowTracker.CreateSnapshot();
            foreground = foregroundTracker.Current;
            powerSaver = _powerSaver;
            peekActive = _peekActive;
            transition = _stateReducer.Reduce(
                new TaskbarStateInput(
                    powerSaver,
                    _taskViewActive,
                    peekActive,
                    foreground.StartMenuMonitor != 0,
                    foreground.SearchMonitor != 0,
                    windows.HasMaximizedWindows,
                    windows.HasVisibleWindows,
                    foreground.Window,
                    PInvoke.IsZoomed(foreground.Window)
                )
            );
        }

        LogReduction(transition, windows, foreground, peekActive);
        if (transition.ShouldPublish)
        {
            publish(transition);
        }
    }

    private void LogReduction(
        TaskbarStateTransition transition,
        TaskbarWindowSnapshot windows,
        TaskbarForegroundSnapshot foreground,
        bool peekActive
    )
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            CoreLogMessages.TaskbarStateCalculating(
                logger,
                transition.ForegroundWindow,
                windows.MonitorCount,
                windows.NormalWindowCount,
                windows.MaximizedWindowCount,
                peekActive,
                foreground.StartMenuMonitor,
                foreground.SearchMonitor
            );
        }

        if (transition.StateChanged)
        {
            CoreLogMessages.TaskbarStateChanged(
                logger,
                transition.PreviousState,
                transition.State
            );
        }

        if (transition.ShouldPublish)
        {
            CoreLogMessages.TaskbarStatePublished(
                logger,
                transition.State,
                transition.ForegroundWindow,
                transition.ForegroundWindowMaximized,
                transition.StateChanged,
                transition.ForegroundWindowChanged,
                transition.ForegroundWindowMaximizedChanged
            );
        }
    }
}