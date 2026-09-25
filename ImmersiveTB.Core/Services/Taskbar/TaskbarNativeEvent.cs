using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Identifies a raw taskbar-related observation produced by native observers.
/// </summary>
internal enum TaskbarNativeEventKind
{
    None,
    Initialize,
    PowerSaverChanged,
    PeekChanged,
    WindowUpserted,
    WindowLocationChanged,
    WindowRemoved,
    ForegroundChanged
}

/// <summary>
///     Carries one raw native observation into the taskbar state implementation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TaskbarNativeEvent(
    TaskbarNativeEventKind Kind,
    HWND Window = default,
    bool IsActive = false
);