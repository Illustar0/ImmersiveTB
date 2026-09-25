using System.Drawing;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Helpers;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable MA0051 // Animation keeps cancellation and native updates in one flow.

namespace ImmersiveTB.Core.Services.Taskbar;

[SupportedOSPlatform("windows5.0")]
public sealed class TranslucentTbManagerService(
    ILogger<TranslucentTbManagerService> logger,
    IOptionsMonitor<TaskbarAppearanceOptions> appearanceOptions
)
    : ITaskbarManagerService, IDisposable
{
    private readonly Lock _animationLock = new();
    private readonly Lock _targetLock = new();
    private CancellationTokenSource? _animationCts;
    private TaskbarState _currentState = TaskbarState.Desktop;
    private long _lastDiscoveryAttempt;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private bool? _reportedAvailability;
    private HWND _translucentTbHwnd;
    private uint _wmTtbApplyColorPreview;

    public event EventHandler<TaskbarAvailabilityChangedEventArgs>? AvailabilityChanged;

    public IntPtr Hwnd
    {
        get;
        private set;
    }

    public Color CurrentColor
    {
        get;
        private set;
    } = Color.Transparent;

    public bool IsAvailable
    {
        get
        {
            lock (_targetLock)
            {
                return IsValidWindow(_translucentTbHwnd);
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        CoreLogMessages.ServiceStarting(logger, nameof(TranslucentTbManagerService));
        _wmTtbApplyColorPreview = PInvoke.RegisterWindowMessage("TTB_ApplyColorPreview");
        RefreshTargets();
        _reconnectCts = new CancellationTokenSource();
        _reconnectTask = MonitorTargetsAsync(_reconnectCts.Token);
        CoreLogMessages.ServiceStarted(logger, nameof(TranslucentTbManagerService));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancelCurrentAnimation();
        if (_reconnectCts is { } reconnectCts)
        {
            await reconnectCts.CancelAsync().ConfigureAwait(false);
        }

        if (_reconnectTask is not null)
        {
            try
            {
                await _reconnectTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _reconnectTask = null;
            }
        }

        _reconnectCts?.Dispose();
        _reconnectCts = null;
        _reconnectTask = null;
        CoreLogMessages.ServiceStopped(logger, nameof(TranslucentTbManagerService));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancelCurrentAnimation();
        if (_reconnectCts is { } reconnectCts)
        {
            reconnectCts.Cancel();
            reconnectCts.Dispose();
            _reconnectCts = null;
        }

        _reconnectTask = null;
    }

    public bool RefreshTargets()
    {
        Volatile.Write(ref _lastDiscoveryAttempt, Environment.TickCount64);
        bool workerFound;
        bool availabilityChanged;
        lock (_targetLock)
        {
            _translucentTbHwnd = PInvoke.FindWindow(
                "TTB_WorkerWindow",
                "TTB_WorkerWindow"
            );
            Hwnd = PInvoke.FindWindow("Shell_TrayWnd");

            workerFound = IsValidWindow(_translucentTbHwnd);
            availabilityChanged = _reportedAvailability != workerFound;
            _reportedAvailability = workerFound;
        }

        if (availabilityChanged)
        {
            CoreLogMessages.TaskbarTargetsDiscovered(
                logger,
                workerFound,
                Hwnd != IntPtr.Zero
            );
            if (!workerFound)
            {
                CoreLogMessages.TranslucentTbUnavailable(logger);
            }

            AvailabilityChanged?.Invoke(
                this,
                new TaskbarAvailabilityChangedEventArgs(workerFound)
            );
        }

        return workerFound;
    }

    public Task SetColorAsync(
        TaskbarState state,
        Color targetColor,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _currentState = state;
        CancelCurrentAnimation();
        ApplyColor(state, targetColor);
        return Task.CompletedTask;
    }

    public async Task SetColorAnimatedAsync(
        TaskbarState state,
        Color targetColor,
        EasingType easing,
        TimeSpan duration,
        CancellationToken cancellationToken = default
    )
    {
        _currentState = state;
        CancelCurrentAnimation();

        var animationCts = new CancellationTokenSource();
        CancellationTokenSource linkedCts;
        lock (_animationLock)
        {
            _animationCts = animationCts;
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                animationCts.Token,
                cancellationToken
            );
        }

        try
        {
            CoreLogMessages.ColorAnimationStarted(
                logger,
                targetColor.ToArgb(),
                duration.TotalMilliseconds,
                easing.ToString()
            );
            var startColor = CurrentColor;
            var easingFunc = Easing.GetEasingFunction(easing);
            var startTime = Environment.TickCount64;
            var durationMs = (long)duration.TotalMilliseconds;
            var frameRate = Math.Clamp(
                appearanceOptions.CurrentValue.ColorAnimationFrameRate,
                1,
                240
            );
            var frameInterval = TimeSpan.FromSeconds(1.0 / frameRate);
            using var frameTimer = new PeriodicTimer(frameInterval);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                var elapsed = Environment.TickCount64 - startTime;
                var progress = durationMs > 0
                    ? Math.Min(1.0, (double)elapsed / durationMs)
                    : 1.0;
                if (progress >= 1.0)
                {
                    break;
                }

                ApplyColor(
                    state,
                    InterpolateColor(startColor, targetColor, progress, easingFunc)
                );

                var remaining = TimeSpan.FromMilliseconds(durationMs - elapsed);
                if (remaining <= frameInterval)
                {
                    await Task.Delay(remaining, linkedCts.Token).ConfigureAwait(false);
                    break;
                }

                if (
                    !await frameTimer
                        .WaitForNextTickAsync(linkedCts.Token)
                        .ConfigureAwait(false)
                )
                {
                    break;
                }
            }

            if (!linkedCts.Token.IsCancellationRequested)
            {
                ApplyColor(state, targetColor);
            }
        }
        catch (OperationCanceledException)
        {
            CoreLogMessages.ColorAnimationCanceled(logger);
            throw;
        }
        finally
        {
            linkedCts.Dispose();
            animationCts.Dispose();
        }
    }

    private void ApplyColor(TaskbarState state, Color color)
    {
        CurrentColor = color;

        HWND worker;
        lock (_targetLock)
        {
            worker = _translucentTbHwnd;
        }

        if (worker.IsNull)
        {
            if (!EnsureTargets())
            {
                return;
            }

            lock (_targetLock)
            {
                worker = _translucentTbHwnd;
            }
        }

        var rgba =
            ((uint)color.R << 24) | ((uint)color.G << 16) | ((uint)color.B << 8) | color.A;
        if (PInvoke.PostMessage(
                worker,
                _wmTtbApplyColorPreview,
                ToTranslucentTbState(state),
                (nint)rgba
            ))
        {
            return;
        }

        CoreLogMessages.TranslucentTbMessageFailed(logger, state);
        lock (_targetLock)
        {
            _translucentTbHwnd = HWND.Null;
        }
    }

    private static nuint ToTranslucentTbState(TaskbarState state) => state switch
    {
        TaskbarState.Desktop => 0,
        TaskbarState.VisibleWindow => 1,
        TaskbarState.MaximizedWindow => 2,
        TaskbarState.StartMenuOpen => 3,
        TaskbarState.SearchOpen => 4,
        TaskbarState.TaskView => 5,
        TaskbarState.BatterySaver => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    private async Task MonitorTargetsAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!IsAvailable && RefreshTargets())
            {
                ApplyColor(_currentState, CurrentColor);
            }
        }
    }

    private bool EnsureTargets()
    {
        if (IsAvailable)
        {
            return true;
        }

        var now = Environment.TickCount64;
        if (now - Volatile.Read(ref _lastDiscoveryAttempt) < 2000)
        {
            return false;
        }

        return RefreshTargets();
    }

    private static bool IsValidWindow(HWND window) =>
        !window.IsNull && PInvoke.IsWindow(window);

    private static Color InterpolateColor(
        Color start,
        Color end,
        double t,
        Func<double, double> easingFunc
    )
    {
        var easedT = easingFunc(Math.Clamp(t, 0, 1));
        return Color.FromArgb(
            Math.Clamp((int)(start.A + (end.A - start.A) * easedT), 0, 255),
            Math.Clamp((int)(start.R + (end.R - start.R) * easedT), 0, 255),
            Math.Clamp((int)(start.G + (end.G - start.G) * easedT), 0, 255),
            Math.Clamp((int)(start.B + (end.B - start.B) * easedT), 0, 255)
        );
    }

    private void CancelCurrentAnimation()
    {
        lock (_animationLock)
        {
            if (_animationCts is { } cts)
            {
                _ = cts.CancelAsync();
                _animationCts = null;
            }
        }
    }
}