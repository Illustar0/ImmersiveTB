using ImmersiveTB.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppNotifications;

namespace ImmersiveTB.Services;

/// <summary>
///     Registers app-notification activation and restores the main window when invoked.
/// </summary>
public sealed class AppNotificationService(
    ILogger<AppNotificationService> logger
) : IDisposable
{
    private bool _isRegistered;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_isRegistered)
        {
            return;
        }

        AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
        AppNotificationManager.Default.Unregister();
        _isRegistered = false;
        AppLogMessages.NotificationsUnregistered(logger);
    }

    /// <summary>
    ///     Occurs when the user invokes an app notification.
    /// </summary>
    public event EventHandler? Invoked;

    /// <summary>
    ///     Registers the application with the Windows notification manager.
    /// </summary>
    public void Initialize()
    {
        if (_isRegistered)
        {
            return;
        }

        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();
        _isRegistered = true;
        AppLogMessages.NotificationsRegistered(logger);
    }

    private void OnNotificationInvoked(
        AppNotificationManager sender,
        AppNotificationActivatedEventArgs args
    ) => Invoked?.Invoke(this, EventArgs.Empty);
}