using System.ComponentModel;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Describes foreground-window state and shell surfaces derived from its process.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TaskbarForegroundSnapshot(
    HWND Window,
    nint StartMenuMonitor,
    nint SearchMonitor
);

/// <summary>
///     Owns foreground-window process classification for Start and Search surfaces.
/// </summary>
internal sealed class TaskbarForegroundTracker(ILogger logger)
{
    /// <summary>Gets the latest foreground-derived state.</summary>
    public TaskbarForegroundSnapshot Current
    {
        get;
        private set;
    }

    /// <summary>
    ///     Refreshes the foreground state from Windows without logging a transition.
    /// </summary>
    public bool Refresh() => Update(PInvoke.GetForegroundWindow(), false);

    /// <summary>
    ///     Updates the foreground state from a native foreground event.
    /// </summary>
    public bool Update(HWND window, bool logChange)
    {
        var previousWindow = Current.Window;
        if (window == previousWindow && !window.IsNull)
        {
            return false;
        }

        var previousProcessName = GetProcessName(previousWindow, "<Exited>");
        var processName = GetProcessName(window, "<Unknown>");
        Current = new TaskbarForegroundSnapshot(
            window,
            GetShellMonitor(window, processName, ShellSurface.StartMenu),
            GetShellMonitor(window, processName, ShellSurface.Search)
        );

        if (logChange)
        {
            CoreLogMessages.ForegroundWindowChanged(
                logger,
                previousWindow,
                previousProcessName,
                window,
                processName
            );
        }

        return true;
    }

    private static nint GetShellMonitor(
        HWND window,
        string processName,
        ShellSurface expectedSurface
    )
    {
        if (GetShellSurface(processName) != expectedSurface)
        {
            return 0;
        }

        return PInvoke.MonitorFromWindow(
            window,
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST
        );
    }

    private static ShellSurface GetShellSurface(string processName) =>
        processName switch
        {
            "StartMenuExperienceHost" => ShellSurface.StartMenu,
            "SearchHost" or "SearchUI" or "SearchApp" => ShellSurface.Search,
            _ => ShellSurface.None
        };

    private static string GetProcessName(HWND window, string fallback)
    {
        if (window.IsNull)
        {
            return fallback;
        }

        PInvoke.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0)
        {
            return fallback;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception exception) when (
            exception
                is ArgumentException
                or InvalidOperationException
                or Win32Exception
        )
        {
            return fallback;
        }
    }

    private enum ShellSurface
    {
        None,
        StartMenu,
        Search
    }
}