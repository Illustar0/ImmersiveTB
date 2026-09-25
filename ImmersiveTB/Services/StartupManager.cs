using System.ComponentModel;
using Windows.ApplicationModel;
using ImmersiveTB.Helpers;
using ImmersiveTB.Logging;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Services;

/// <summary>Owns the packaged application's startup registration state.</summary>
public sealed class StartupManager(ILogger<StartupManager> logger) : INotifyPropertyChanged
{
    private const string StartupTaskId = "ImmersiveTBStartup";
    private StartupTaskState _startupState = StartupTaskState.DisabledByPolicy;
    private bool _hasStartupState;

    /// <summary>Gets whether Windows starts the application at sign-in.</summary>
    public bool IsEnabled => _startupState is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

    /// <summary>Gets whether the startup setting can be changed.</summary>
    public bool CanToggle => _hasStartupState
                             && _startupState is StartupTaskState.Disabled or StartupTaskState.Enabled;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Reads the startup registration before the tray icon is shown.</summary>
    public async Task InitializeAsync()
    {
        if (!RuntimeHelper.IsMSIX)
        {
            return;
        }

        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            _startupState = task.State;
            _hasStartupState = true;
            NotifyStateChanged();
        }
        catch (Exception exception)
        {
            AppLogMessages.StartupTaskOperationFailed(logger, exception);
        }
    }

    /// <summary>Changes the startup registration when Windows permits it.</summary>
    public async Task ToggleAsync()
    {
        _hasStartupState = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanToggle)));
        if (!RuntimeHelper.IsMSIX)
        {
            return;
        }

        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (task.State == StartupTaskState.Disabled)
            {
                _startupState = await task.RequestEnableAsync();
            }
            else
            {
                if (task.State == StartupTaskState.Enabled)
                {
                    task.Disable();
                }

                _startupState = task.State;
            }

            _hasStartupState = true;
        }
        catch (Exception exception)
        {
            AppLogMessages.StartupTaskOperationFailed(logger, exception);
        }

        NotifyStateChanged();
    }

    /// <summary>Notifies bindings after Windows reports the startup state.</summary>
    private void NotifyStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanToggle)));
    }
}
