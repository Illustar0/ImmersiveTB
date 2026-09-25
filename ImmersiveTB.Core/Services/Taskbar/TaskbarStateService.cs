// Copyright (c) 2025 Illustar0
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Portions of this code are derived from TranslucentTB (https://github.com/TranslucentTB/TranslucentTB)
// licensed under the GNU General Public License v3.0.

using System.Runtime.Versioning;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Exposes taskbar state while delegating native observation, window tracking,
///     and state reduction to focused internal modules.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class TaskbarStateService : ITaskbarStateService
{
    private readonly TaskbarNativeEventSource _eventSource;
    private readonly ILogger<TaskbarStateService> _logger;
    private readonly TaskbarStateController _stateController;

    /// <summary>
    ///     Initializes the taskbar state facade and its internal implementation modules.
    /// </summary>
    public TaskbarStateService(
        ILogger<TaskbarStateService> logger,
        IDispatcherService dispatcherService
    )
    {
        _logger = logger;
        var windowClassifier = new TaskbarWindowClassifier(logger);
        var windowTracker = new TaskbarWindowTracker(windowClassifier, logger);
        var foregroundTracker = new TaskbarForegroundTracker(logger);
        _stateController = new TaskbarStateController(
            windowTracker,
            foregroundTracker,
            dispatcherService,
            PublishTransition,
            logger
        );
        _eventSource = new TaskbarNativeEventSource(
            _stateController.Observe,
            logger
        );
    }

    /// <inheritdoc />
    public event EventHandler<TaskbarStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public TaskbarState CurrentState => _stateController.CurrentState;

    /// <inheritdoc />
    public IntPtr CurrentForegroundWindowHwnd => _stateController.ForegroundWindow;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        CoreLogMessages.ServiceStarting(_logger, nameof(TaskbarStateService));
        await _eventSource.StartAsync(cancellationToken).ConfigureAwait(false);
        CoreLogMessages.ServiceStarted(_logger, nameof(TaskbarStateService));
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _eventSource.StopAsync(cancellationToken).ConfigureAwait(false);
        CoreLogMessages.ServiceStopped(_logger, nameof(TaskbarStateService));
    }

    /// <inheritdoc />
    public void Refresh() => _stateController.RefreshWindows();

    /// <summary>
    ///     Preserves the upstream task-view observation seam.
    /// </summary>
    internal void OnTaskViewVisibilityChange(bool visible) =>
        _stateController.SetTaskViewActive(visible);

    private void PublishTransition(TaskbarStateTransition transition) =>
        StateChanged?.Invoke(
            this,
            new TaskbarStateChangedEventArgs(transition.State, transition.ForegroundWindow)
        );
}