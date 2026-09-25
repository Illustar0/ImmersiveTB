using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Hosts native observers on a dedicated message-pump thread.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
internal sealed class TaskbarNativeEventSource
{
    private readonly ILogger _logger;
    private readonly Action<TaskbarNativeEvent> _observer;
    private readonly PowerSaverMonitor _powerSaverMonitor;

    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private readonly TaskCompletionSource _stopped = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private readonly TaskbarWinEventMonitor _winEventMonitor;

    private Thread? _hookThread;
    private uint _hookThreadId;

    /// <summary>
    ///     Initializes the native observation host.
    /// </summary>
    public TaskbarNativeEventSource(
        Action<TaskbarNativeEvent> observer,
        ILogger logger
    )
    {
        _observer = observer;
        _logger = logger;
        _powerSaverMonitor = new PowerSaverMonitor(
            powerSaver =>
                Publish(
                    new TaskbarNativeEvent(
                        TaskbarNativeEventKind.PowerSaverChanged,
                        IsActive: powerSaver
                    )
                ),
            logger
        );
        _winEventMonitor = new TaskbarWinEventMonitor(Publish, logger);
    }

    /// <summary>
    ///     Starts the dedicated hook thread and waits until native observers are ready.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_hookThread is not null)
        {
            throw new InvalidOperationException("The taskbar event source is already started.");
        }

        _hookThread = new Thread(RunHookThread)
        {
            IsBackground = true,
            Name = "ImmersiveTB.TaskbarState.HookThread"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        return _started.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    ///     Requests hook-thread shutdown and waits for native resources to be released.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hookThread is null)
        {
            return;
        }

        var threadId = Volatile.Read(ref _hookThreadId);
        if (threadId != 0)
        {
            PInvoke.PostThreadMessage(threadId, PInvoke.WM_QUIT, 0, 0);
        }

        await _stopped.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void RunHookThread()
    {
        _hookThreadId = PInvoke.GetCurrentThreadId();
        CoreLogMessages.HookThreadStarted(_logger, _hookThreadId);

        try
        {
            _powerSaverMonitor.Start();
            _winEventMonitor.Start();
            Publish(new TaskbarNativeEvent(TaskbarNativeEventKind.Initialize));
            _started.TrySetResult();

            while (true)
            {
                var result = PInvoke.GetMessage(out var message, HWND.Null, 0, 0);
                if (result.Value == -1)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (result.Value == 0)
                {
                    break;
                }

                PInvoke.TranslateMessage(message);
                PInvoke.DispatchMessage(message);
            }
        }
        catch (Exception exception)
        {
            CoreLogMessages.HookThreadFailed(_logger, exception);
            _started.TrySetException(exception);
        }
        finally
        {
            _winEventMonitor.Stop();
            _powerSaverMonitor.Stop();
            _stopped.TrySetResult();
            CoreLogMessages.HookThreadExited(_logger);
        }
    }

    private void Publish(TaskbarNativeEvent observation) => _observer(observation);
}