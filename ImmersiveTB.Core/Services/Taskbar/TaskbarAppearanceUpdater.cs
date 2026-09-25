using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Serializes taskbar appearance requests and owns their cancellation, sampling,
///     animation, and Windows theme side effects.
/// </summary>
public sealed class TaskbarAppearanceUpdater(
    IColorSamplerService colorSamplerService,
    ITaskbarManagerService taskbarManagerService,
    ISystemThemeManagerService systemThemeManagerService,
    DynamicTaskbarService dynamicTaskbarService,
    IOptionsMonitor<TaskbarAppearanceOptions> appearanceOptions,
    ILogger<TaskbarAppearanceUpdater> logger
)
{
    private readonly Lock _updateLock = new();
    private Task _activeUpdate = Task.CompletedTask;
    private CancellationTokenSource? _activeUpdateCancellation;
    private bool _stopped;

    /// <summary>
    ///     Submits the latest observed taskbar state and supersedes any older update.
    /// </summary>
    /// <param name="state">The newly reduced taskbar state.</param>
    /// <param name="foregroundWindow">The foreground window associated with the state.</param>
    public void Submit(TaskbarState state, IntPtr foregroundWindow)
    {
        CancellationTokenSource? supersededUpdate;
        lock (_updateLock)
        {
            if (_stopped)
            {
                return;
            }

            supersededUpdate = _activeUpdateCancellation;
            _activeUpdateCancellation = null;
            if (dynamicTaskbarService.IsEnabled && state == TaskbarState.MaximizedWindow)
            {
                var cancellation = new CancellationTokenSource();
                _activeUpdateCancellation = cancellation;
                _activeUpdate = ApplyAfterAsync(
                    _activeUpdate,
                    state,
                    foregroundWindow,
                    cancellation
                );
            }
            else
            {
                _activeUpdate = RestoreAfterAsync(_activeUpdate);
            }
        }

        _ = supersededUpdate?.CancelAsync();
    }

    /// <summary>
    ///     Cancels the current update without scheduling a replacement.
    /// </summary>
    public void Cancel()
    {
        CancellationTokenSource? cancellation;
        lock (_updateLock)
        {
            cancellation = _activeUpdateCancellation;
            _activeUpdateCancellation = null;
            _activeUpdate = RestoreAfterAsync(_activeUpdate);
        }

        _ = cancellation?.CancelAsync();
    }

    /// <summary>Restores the previous Windows theme after any active appearance update.</summary>
    public void RestoreTheme()
    {
        lock (_updateLock)
        {
            if (!_stopped)
            {
                _activeUpdate = RestoreAfterAsync(_activeUpdate);
            }
        }
    }

    /// <summary>
    ///     Stops accepting updates, cancels active work, and waits for the serialized queue to drain.
    /// </summary>
    /// <param name="cancellationToken">Limits how long shutdown waits.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? cancellation;
        Task activeUpdate;
        lock (_updateLock)
        {
            _stopped = true;
            cancellation = _activeUpdateCancellation;
            _activeUpdateCancellation = null;
            activeUpdate = _activeUpdate;
        }

        _ = cancellation?.CancelAsync();
        try
        {
            await activeUpdate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CoreLogMessages.AppearanceUpdateCanceled(logger);
        }

        if (activeUpdate.IsCompleted && systemThemeManagerService.RestoreSystemTheme())
        {
            await systemThemeManagerService
                .BroadcastSettingChange(taskbarManagerService.Hwnd)
                .ConfigureAwait(false);
        }
    }

    private async Task ApplyAfterAsync(
        Task previousUpdate,
        TaskbarState state,
        IntPtr foregroundWindow,
        CancellationTokenSource cancellation
    )
    {
        try
        {
            await previousUpdate.ConfigureAwait(false);
            await ApplyAsync(state, foregroundWindow, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            CoreLogMessages.AppearanceUpdateCanceled(logger);
        }
        catch (Exception exception)
        {
            CoreLogMessages.AppearanceUpdateFailed(logger, state, exception);
        }
        finally
        {
            lock (_updateLock)
            {
                if (ReferenceEquals(_activeUpdateCancellation, cancellation))
                {
                    _activeUpdateCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private async Task RestoreAfterAsync(Task previousUpdate)
    {
        try
        {
            await previousUpdate.ConfigureAwait(false);
            if (systemThemeManagerService.RestoreSystemTheme())
            {
                await systemThemeManagerService
                    .BroadcastSettingChange(taskbarManagerService.Hwnd)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            CoreLogMessages.SystemThemeRestoreFailed(logger, exception);
        }
    }

    private async Task ApplyAsync(
        TaskbarState state,
        IntPtr foregroundWindow,
        CancellationToken cancellationToken
    )
    {
        CoreLogMessages.ApplyingTaskbarAppearance(logger, state, foregroundWindow);
        var appearance = appearanceOptions.CurrentValue;
        var samplingDelay = appearance.ColorSamplingDelayMs;
        var animationEnabled = appearance.ColorAnimationEnabled;
        var animationEasing = appearance.ColorEasing;
        var animationDuration = TimeSpan.FromMilliseconds(
            appearance.ColorAnimationDurationMs
        );

        if (samplingDelay > 0)
        {
            await Task.Delay(samplingDelay, cancellationToken).ConfigureAwait(false);
        }

        var color = await colorSamplerService
            .SampleAsync(cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!dynamicTaskbarService.IsEnabled)
        {
            return;
        }

        if (animationEnabled)
        {
            await taskbarManagerService
                .SetColorAnimatedAsync(
                    state,
                    color,
                    animationEasing,
                    animationDuration,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        else
        {
            await taskbarManagerService
                .SetColorAsync(state, color, cancellationToken)
                .ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!dynamicTaskbarService.IsDynamicThemeEnabled)
        {
            return;
        }

        if (!systemThemeManagerService.SetSystemTheme(
                systemThemeManagerService.GetAdaptedTheme(color)
            ))
        {
            return;
        }

        await systemThemeManagerService
            .BroadcastSettingChange(taskbarManagerService.Hwnd)
            .ConfigureAwait(false);
    }
}