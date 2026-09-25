using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Hosting;

namespace ImmersiveTB.Core.Contracts.Services;

public interface ITaskbarStateService : IHostedService
{
    TaskbarState CurrentState
    {
        get;
    }

    IntPtr CurrentForegroundWindowHwnd
    {
        get;
    }


    event EventHandler<TaskbarStateChangedEventArgs>? StateChanged;

    /// <summary>
    ///     Rebuilds the tracked window set and republishes the current state.
    /// </summary>
    void Refresh();
}

public sealed class TaskbarStateChangedEventArgs(TaskbarState state, IntPtr foregroundWindow) : EventArgs
{
    public TaskbarState State
    {
        get;
    } = state;

    public IntPtr ForegroundWindow
    {
        get;
    } = foregroundWindow;
}