using System.Runtime.InteropServices;
using ImmersiveTB.Core.Models;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Contains the platform-independent inputs needed to reduce raw taskbar observations.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TaskbarStateInput(
    bool PowerSaverActive,
    bool TaskViewActive,
    bool PeekActive,
    bool StartMenuOpen,
    bool SearchOpen,
    bool HasMaximizedWindows,
    bool HasVisibleWindows,
    IntPtr ForegroundWindow,
    bool ForegroundWindowMaximized
);

/// <summary>
///     Describes one observable state reduction and why it should be published.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TaskbarStateTransition(
    TaskbarState PreviousState,
    TaskbarState State,
    IntPtr ForegroundWindow,
    bool ForegroundWindowMaximized,
    bool StateChanged,
    bool ForegroundWindowChanged,
    bool ForegroundWindowMaximizedChanged
)
{
    /// <summary>
    ///     Gets whether callers should receive this transition.
    /// </summary>
    public bool ShouldPublish =>
        StateChanged || ForegroundWindowChanged || ForegroundWindowMaximizedChanged;
}

/// <summary>
///     Reduces repeatable taskbar observations into state transitions without Win32 hooks.
/// </summary>
internal sealed class TaskbarStateReducer
{
    private IntPtr _lastPublishedForegroundWindow;
    private bool _lastPublishedForegroundWindowMaximized;

    /// <summary>
    ///     Gets the latest reduced taskbar state.
    /// </summary>
    public TaskbarState CurrentState
    {
        get;
        private set;
    } = TaskbarState.Desktop;

    /// <summary>
    ///     Reduces a raw observation and records it as the latest state.
    /// </summary>
    /// <param name="input">The current platform observations.</param>
    /// <returns>The resulting transition and publication decision.</returns>
    public TaskbarStateTransition Reduce(TaskbarStateInput input)
    {
        var previousState = CurrentState;
        var state = CalculateState(input);
        var transition = new TaskbarStateTransition(
            previousState,
            state,
            input.ForegroundWindow,
            input.ForegroundWindowMaximized,
            state != previousState,
            input.ForegroundWindow != _lastPublishedForegroundWindow,
            input.ForegroundWindowMaximized != _lastPublishedForegroundWindowMaximized
        );

        CurrentState = state;
        if (transition.ShouldPublish)
        {
            _lastPublishedForegroundWindow = input.ForegroundWindow;
            _lastPublishedForegroundWindowMaximized = input.ForegroundWindowMaximized;
        }

        return transition;
    }

    private static TaskbarState CalculateState(TaskbarStateInput input)
    {
        if (input.PowerSaverActive)
        {
            return TaskbarState.BatterySaver;
        }

        if (input.TaskViewActive)
        {
            return TaskbarState.TaskView;
        }

        if (input.PeekActive)
        {
            return TaskbarState.Desktop;
        }

        if (input.StartMenuOpen)
        {
            return TaskbarState.StartMenuOpen;
        }

        if (input.SearchOpen)
        {
            return TaskbarState.SearchOpen;
        }

        if (input.HasMaximizedWindows)
        {
            return TaskbarState.MaximizedWindow;
        }

        return input.HasVisibleWindows
            ? TaskbarState.VisibleWindow
            : TaskbarState.Desktop;
    }
}