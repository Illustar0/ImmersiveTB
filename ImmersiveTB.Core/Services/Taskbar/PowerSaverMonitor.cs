using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;

#pragma warning disable S6640 // Win32 window callbacks require unsafe pointers.

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Owns the message-only window and Windows power-saver notifications.
/// </summary>
internal sealed class PowerSaverMonitor(Action<bool> observer, ILogger logger)
{
    private const string WindowClassName = "ImmersiveTBPowerNotificationWindow";

    private static readonly Guid PowerSavingStatus = new(
        "E00958C0-C213-4ACE-AC77-FECCED2EEEA5"
    );

    private static PowerSaverMonitor? _activeMonitor;
    private readonly ILogger _logger = logger;
    private readonly Action<bool> _observer = observer;
    private HPOWERNOTIFY _notificationHandle;
    private HWND _window;

    /// <summary>
    ///     Creates the notification window and starts reporting power-saver changes.
    /// </summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _activeMonitor, this, null) is not null)
        {
            throw new InvalidOperationException("Only one power-saver monitor can be active.");
        }

        PublishInitialState();
        CreateMessageWindow();
        RegisterNotification();
    }

    /// <summary>
    ///     Releases the notification registration and message-only window.
    /// </summary>
    public void Stop()
    {
        if (_notificationHandle != 0)
        {
            PInvoke.UnregisterPowerSettingNotification(_notificationHandle);
            _notificationHandle = default;
        }

        if (!_window.IsNull)
        {
            PInvoke.DestroyWindow(_window);
            _window = HWND.Null;
        }

        Interlocked.CompareExchange(ref _activeMonitor, null, this);
    }

    private void PublishInitialState()
    {
        if (PInvoke.GetSystemPowerStatus(out var powerStatus))
        {
            var powerSaver = powerStatus.SystemStatusFlag != 0;
            CoreLogMessages.InitialPowerState(_logger, powerSaver);
            _observer(powerSaver);
        }
        else
        {
            CoreLogMessages.PowerStatusUnavailable(_logger);
        }
    }

    private void CreateMessageWindow()
    {
        unsafe
        {
            fixed (char* className = WindowClassName)
            fixed (char* windowName = "ImmersiveTB Power Notification Window")
            {
                var windowClass = new WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    lpfnWndProc = &WndProc,
                    hInstance = PInvoke.GetModuleHandle((PCWSTR)null),
                    lpszClassName = className
                };

                if (PInvoke.RegisterClassEx(in windowClass) == 0)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 1410)
                    {
                        CoreLogMessages.MessageWindowRegistrationFailed(_logger, error);
                        return;
                    }
                }

                _window = PInvoke.CreateWindowEx(
                    WINDOW_EX_STYLE.WS_EX_LEFT,
                    className,
                    windowName,
                    WINDOW_STYLE.WS_OVERLAPPED,
                    0,
                    0,
                    0,
                    0,
                    (HWND)(-3),
                    HMENU.Null,
                    windowClass.hInstance,
                    null
                );
            }
        }

        if (_window.IsNull)
        {
            CoreLogMessages.MessageWindowCreationFailed(_logger);
        }
    }

    private void RegisterNotification()
    {
        if (_window.IsNull)
        {
            return;
        }

        unsafe
        {
            var setting = PowerSavingStatus;
            _notificationHandle = PInvoke.RegisterPowerSettingNotification(
                new HANDLE(_window.Value),
                &setting,
                REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE
            );
        }

        if (_notificationHandle == 0)
        {
            CoreLogMessages.PowerNotificationRegistrationFailed(_logger);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        const uint wmPowerBroadcast = 0x0218;
        const uint powerSettingChange = 0x8013;

        if (
            message == wmPowerBroadcast
            && (uint)wParam.Value == powerSettingChange
            && Volatile.Read(ref _activeMonitor) is { } monitor
        )
        {
            try
            {
                monitor.HandlePowerBroadcast(lParam);
            }
            catch (Exception exception)
            {
                CoreLogMessages.NativeEventProcessingFailed(
                    monitor._logger,
                    message,
                    exception
                );
            }

            return new LRESULT(1);
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }

    private void HandlePowerBroadcast(LPARAM parameter)
    {
        unsafe
        {
            var data = (byte*)parameter.Value;
            if (*(Guid*)data != PowerSavingStatus || *(uint*)(data + 16) < sizeof(uint))
            {
                return;
            }

            var powerSaver = *(uint*)(data + 20) != 0;
            CoreLogMessages.PowerStateChanged(_logger, powerSaver);
            _observer(powerSaver);
        }
    }
}