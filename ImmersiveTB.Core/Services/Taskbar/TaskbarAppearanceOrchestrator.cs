using System.ComponentModel;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Adapts taskbar-state and runtime-control events into serialized appearance requests.
/// </summary>
public sealed class TaskbarAppearanceOrchestrator(
    ITaskbarStateService taskbarStateService,
    DynamicTaskbarService dynamicTaskbarService,
    TaskbarAppearanceUpdater appearanceUpdater,
    ILogger<TaskbarAppearanceOrchestrator> logger
) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        CoreLogMessages.ServiceStarting(logger, nameof(TaskbarAppearanceOrchestrator));
        taskbarStateService.StateChanged += OnTaskbarStateChanged;
        dynamicTaskbarService.PropertyChanged += OnDynamicTaskbarPropertyChanged;
        appearanceUpdater.Submit(
            taskbarStateService.CurrentState,
            taskbarStateService.CurrentForegroundWindowHwnd
        );
        CoreLogMessages.ServiceStarted(logger, nameof(TaskbarAppearanceOrchestrator));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        taskbarStateService.StateChanged -= OnTaskbarStateChanged;
        dynamicTaskbarService.PropertyChanged -= OnDynamicTaskbarPropertyChanged;
        await appearanceUpdater.StopAsync(cancellationToken).ConfigureAwait(false);
        CoreLogMessages.ServiceStopped(logger, nameof(TaskbarAppearanceOrchestrator));
    }

    private void OnTaskbarStateChanged(
        object? sender,
        TaskbarStateChangedEventArgs args
    ) => appearanceUpdater.Submit(args.State, args.ForegroundWindow);

    private void OnDynamicTaskbarPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args
    )
    {
        if (string.Equals(args.PropertyName, nameof(DynamicTaskbarService.IsEnabled), StringComparison.Ordinal))
        {
            if (!dynamicTaskbarService.IsEnabled)
            {
                appearanceUpdater.Cancel();
                return;
            }

            appearanceUpdater.Submit(
                taskbarStateService.CurrentState,
                taskbarStateService.CurrentForegroundWindowHwnd
            );
        }
        else if (string.Equals(
                     args.PropertyName,
                     nameof(DynamicTaskbarService.IsDynamicThemeEnabled),
                     StringComparison.Ordinal
                 ))
        {
            if (dynamicTaskbarService.IsDynamicThemeEnabled)
            {
                appearanceUpdater.Submit(
                    taskbarStateService.CurrentState,
                    taskbarStateService.CurrentForegroundWindowHwnd
                );
            }
            else
            {
                appearanceUpdater.RestoreTheme();
            }
        }
    }
}