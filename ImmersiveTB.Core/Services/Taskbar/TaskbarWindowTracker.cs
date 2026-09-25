using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;

#pragma warning disable S6640 // Win32 callbacks require unsafe function pointers.

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Summarizes the tracked application windows used by taskbar state reduction.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TaskbarWindowSnapshot(
    int MonitorCount,
    int NormalWindowCount,
    int MaximizedWindowCount
)
{
    /// <summary>Gets whether any tracked window is maximized.</summary>
    public bool HasMaximizedWindows => MaximizedWindowCount > 0;

    /// <summary>Gets whether any visible application window is tracked.</summary>
    public bool HasVisibleWindows => NormalWindowCount > 0 || MaximizedWindowCount > 0;
}

/// <summary>
///     Owns the monitor-aware catalog of accepted visible application windows.
/// </summary>
internal sealed class TaskbarWindowTracker(
    TaskbarWindowClassifier classifier,
    ILogger logger
)
{
    private readonly Dictionary<nint, int> _monitorWindowCounts = [];
    private readonly Dictionary<nint, TrackedWindow> _windows = [];
    private int _maximizedWindowCount;
    private int _normalWindowCount;

    /// <summary>
    ///     Rebuilds the complete tracked-window catalog from top-level desktop windows.
    /// </summary>
    public void Rebuild(HWND foregroundWindow)
    {
        _windows.Clear();
        _monitorWindowCounts.Clear();
        _normalWindowCount = 0;
        _maximizedWindowCount = 0;
        var context = new EnumerationContext(this, foregroundWindow);
        var contextHandle = GCHandle.Alloc(context);
        try
        {
            unsafe
            {
                PInvoke.EnumWindows(
                    &EnumerateWindow,
                    GCHandle.ToIntPtr(contextHandle)
                );
            }

            if (context.Error is { } error)
            {
                throw new InvalidOperationException(
                    "Unable to enumerate desktop windows.",
                    error
                );
            }
        }
        finally
        {
            contextHandle.Free();
        }

        CoreLogMessages.WindowStateInitialized(logger, CreateSnapshot().MonitorCount);
    }

    /// <summary>
    ///     Re-evaluates a window and updates its tracked classification.
    /// </summary>
    /// <returns><see langword="true" /> when the catalog changed.</returns>
    public bool Upsert(HWND window, HWND foregroundWindow)
    {
        var classification = classifier.Classify(window, foregroundWindow);
        if (!classification.Accepted || classification.IsIconic)
        {
            return Remove((nint)window);
        }

        return Store(
            window,
            new TrackedWindow(
                classification.Monitor,
                classification.IsZoomed
            )
        );
    }

    /// <summary>
    ///     Removes a window from the tracked catalog.
    /// </summary>
    /// <returns><see langword="true" /> when the catalog changed.</returns>
    public bool Remove(HWND window) => Remove((nint)window);

    /// <summary>
    ///     Creates an allocation-free aggregate used by state reduction and diagnostics.
    /// </summary>
    public TaskbarWindowSnapshot CreateSnapshot() =>
        new(
            _monitorWindowCounts.Count,
            _normalWindowCount,
            _maximizedWindowCount
        );

    private bool Store(nint windowHandle, TrackedWindow trackedWindow)
    {
        ref var current = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _windows,
            windowHandle,
            out var exists
        );
        if (exists)
        {
            if (current == trackedWindow)
            {
                return false;
            }

            RemoveFromAggregates(current);
        }

        current = trackedWindow;
        AddToAggregates(trackedWindow);
        return true;
    }

    private bool Remove(nint windowHandle)
    {
        if (!_windows.Remove(windowHandle, out var trackedWindow))
        {
            return false;
        }

        RemoveFromAggregates(trackedWindow);
        return true;
    }

    private void AddToAggregates(TrackedWindow window)
    {
        ref var monitorWindowCount = ref CollectionsMarshal.GetValueRefOrAddDefault(
            _monitorWindowCounts,
            window.Monitor,
            out _
        );
        monitorWindowCount++;
        if (window.IsMaximized)
        {
            _maximizedWindowCount++;
        }
        else
        {
            _normalWindowCount++;
        }
    }

    private void RemoveFromAggregates(TrackedWindow window)
    {
        var monitorWindowCount = _monitorWindowCounts[window.Monitor] - 1;
        if (monitorWindowCount == 0)
        {
            _monitorWindowCounts.Remove(window.Monitor);
        }
        else
        {
            _monitorWindowCounts[window.Monitor] = monitorWindowCount;
        }

        if (window.IsMaximized)
        {
            _maximizedWindowCount--;
        }
        else
        {
            _normalWindowCount--;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL EnumerateWindow(HWND window, LPARAM parameter)
    {
        var context = (EnumerationContext?)GCHandle
            .FromIntPtr(parameter.Value)
            .Target;
        if (context is null)
        {
            return false;
        }

        try
        {
            context.Tracker.Upsert(window, context.ForegroundWindow);
            return true;
        }
        catch (Exception exception)
        {
            context.Error = exception;
            return false;
        }
    }

    private sealed class EnumerationContext(
        TaskbarWindowTracker tracker,
        HWND foregroundWindow
    )
    {
        public TaskbarWindowTracker Tracker
        {
            get;
        } = tracker;

        public HWND ForegroundWindow
        {
            get;
        } = foregroundWindow;

        public Exception? Error
        {
            get;
            set;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TrackedWindow(nint Monitor, bool IsMaximized);
}