using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;

#pragma warning disable S6640 // Win32 buffers require unsafe pointers.
#pragma warning disable MA0051 // Classification keeps its native filtering rules together.

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Describes the Win32 attributes used to accept and classify a top-level window.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TaskbarWindowClassification(
    bool Accepted,
    string Reason,
    string ClassName,
    bool IsVisible,
    bool IsZoomed,
    bool IsIconic,
    nint Monitor,
    int Style,
    int ExtendedStyle,
    bool IsForeground
);

/// <summary>
///     Owns Win32 window eligibility rules and their trace diagnostics.
/// </summary>
internal sealed class TaskbarWindowClassifier(ILogger logger)
{
    /// <summary>
    ///     Evaluates whether a window participates in taskbar state and records diagnostics.
    /// </summary>
    public TaskbarWindowClassification Classify(HWND window, HWND foregroundWindow)
    {
        var traceEnabled = logger.IsEnabled(LogLevel.Trace);
        var classification = Evaluate(window, foregroundWindow, traceEnabled);
        if (traceEnabled)
        {
            Trace(window, classification);
        }

        return classification;
    }

    private static TaskbarWindowClassification Evaluate(
        HWND window,
        HWND foregroundWindow,
        bool captureClassName
    )
    {
        var isVisible = PInvoke.IsWindowVisible(window);
        if (!isVisible)
        {
            return Rejected("NotVisible", isVisible);
        }

        var style = PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        var extendedStyle = PInvoke.GetWindowLong(
            window,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE
        );

        const int VisibleStyle = 0x10000000;
        const int ChildStyle = 0x40000000;
        const int ToolWindowStyle = 0x00000080;
        const int NoActivateStyle = 0x08000000;

        if ((style & ChildStyle) != 0)
        {
            return Rejected("ChildWindow", isVisible, style, extendedStyle);
        }

        if ((extendedStyle & ToolWindowStyle) != 0)
        {
            return Rejected("ToolWindow", isVisible, style, extendedStyle);
        }

        if ((extendedStyle & NoActivateStyle) != 0)
        {
            return Rejected("NoActivate", isVisible, style, extendedStyle);
        }

        if ((style & VisibleStyle) == 0)
        {
            return Rejected("VisibleStyleMissing", isVisible, style, extendedStyle);
        }

        if (PInvoke.GetAncestor(window, GET_ANCESTOR_FLAGS.GA_ROOT) != window)
        {
            return Rejected("NotTopLevelRoot", isVisible, style, extendedStyle);
        }

        Span<char> classNameBuffer = stackalloc char[256];
        var classNameLength = GetClassName(window, classNameBuffer);
        var className = classNameBuffer[..classNameLength];
        if (IsShellWindowClass(className))
        {
            return Rejected(
                "ShellWindowClass",
                isVisible,
                style,
                extendedStyle,
                captureClassName ? className.ToString() : string.Empty
            );
        }

        var monitor = PInvoke.MonitorFromWindow(
            window,
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST
        );
        return new TaskbarWindowClassification(
            true,
            "Accepted",
            captureClassName ? className.ToString() : string.Empty,
            isVisible,
            PInvoke.IsZoomed(window),
            PInvoke.IsIconic(window),
            monitor,
            style,
            extendedStyle,
            window == foregroundWindow
        );
    }

    private static TaskbarWindowClassification Rejected(
        string reason,
        bool isVisible,
        int style = 0,
        int extendedStyle = 0,
        string className = ""
    ) =>
        new(
            false,
            reason,
            className,
            isVisible,
            false,
            false,
            0,
            style,
            extendedStyle,
            false
        );

    private static int GetClassName(HWND window, Span<char> buffer)
    {
        unsafe
        {
            fixed (char* className = buffer)
            {
                return Math.Max(0, PInvoke.GetClassName(window, className, buffer.Length));
            }
        }
    }

    private static bool IsShellWindowClass(ReadOnlySpan<char> className) =>
        className.SequenceEqual("Shell_TrayWnd")
        || className.SequenceEqual("Shell_SecondaryTrayWnd")
        || className.SequenceEqual("Progman")
        || className.SequenceEqual("WorkerW")
        || className.SequenceEqual("Windows.UI.Core.CoreWindow");

    private static string GetWindowStateName(TaskbarWindowClassification classification)
    {
        if (!classification.Accepted)
        {
            return "Rejected";
        }

        if (classification.IsZoomed)
        {
            return "Maximized";
        }

        return classification.IsIconic ? "Minimized" : "Normal";
    }

    private void Trace(HWND window, TaskbarWindowClassification classification)
    {
        PInvoke.GetWindowThreadProcessId(window, out var processId);
        CoreLogMessages.WindowEvaluated(
            logger,
            window,
            processId,
            classification.ClassName,
            GetWindowStateName(classification),
            classification.Reason,
            classification.IsForeground,
            classification.IsVisible,
            classification.IsZoomed,
            classification.IsIconic,
            classification.Monitor,
            classification.Style,
            classification.ExtendedStyle
        );
    }
}