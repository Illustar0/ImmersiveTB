using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;

#pragma warning disable S6640 // Win32 event hooks require unsafe function pointers.

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Owns WinEvent hook registration and translates callbacks into typed observations.
/// </summary>
internal sealed class TaskbarWinEventMonitor(
    Action<TaskbarNativeEvent> observer,
    ILogger logger
)
{
    private const uint EventSystemPeekStart = 0x0021;
    private const uint EventSystemPeekEnd = 0x0022;

    private static TaskbarWinEventMonitor? _activeMonitor;
    private readonly List<HWINEVENTHOOK> _eventHooks = [];
    private readonly ILogger _logger = logger;
    private readonly Action<TaskbarNativeEvent> _observer = observer;

    /// <summary>
    ///     Registers all taskbar-related WinEvent hooks on the current thread.
    /// </summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _activeMonitor, this, null) is not null)
        {
            throw new InvalidOperationException("Only one WinEvent monitor can be active.");
        }

        unsafe
        {
            AddHook(EventSystemPeekStart, EventSystemPeekEnd, &OnWinEvent);
            AddHook(PInvoke.EVENT_OBJECT_SHOW, PInvoke.EVENT_OBJECT_HIDE, &OnWinEvent);
            AddHook(
                PInvoke.EVENT_SYSTEM_MINIMIZESTART,
                PInvoke.EVENT_SYSTEM_MINIMIZEEND,
                &OnWinEvent
            );
            AddHook(
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                PInvoke.EVENT_OBJECT_LOCATIONCHANGE,
                &OnWinEvent
            );
            AddHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                &OnWinEvent
            );
            AddHook(PInvoke.EVENT_OBJECT_CREATE, PInvoke.EVENT_OBJECT_DESTROY, &OnWinEvent);
            AddHook(PInvoke.EVENT_OBJECT_CLOAKED, PInvoke.EVENT_OBJECT_UNCLOAKED, &OnWinEvent);
        }

        CoreLogMessages.EventHooksConfigured(_logger, _eventHooks.Count);
    }

    /// <summary>
    ///     Unregisters every active WinEvent hook.
    /// </summary>
    public void Stop()
    {
        foreach (var hook in _eventHooks)
        {
            PInvoke.UnhookWinEvent(hook);
        }

        _eventHooks.Clear();
        Interlocked.CompareExchange(ref _activeMonitor, null, this);
    }

    private unsafe void AddHook(
        uint eventMin,
        uint eventMax,
        delegate* unmanaged[Stdcall]<HWINEVENTHOOK, uint, HWND, int, int, uint, uint, void> callback
    )
    {
        var hook = PInvoke.SetWinEventHook(
            eventMin,
            eventMax,
            HMODULE.Null,
            callback,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT
        );
        if (hook.IsNull)
        {
            CoreLogMessages.EventHookCreationFailed(_logger, eventMin, eventMax);
            return;
        }

        _eventHooks.Add(hook);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnWinEvent(
        HWINEVENTHOOK hook,
        uint eventId,
        HWND window,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTime
    )
    {
        var monitor = Volatile.Read(ref _activeMonitor);
        if (monitor is null)
        {
            return;
        }

        try
        {
            monitor.HandleWinEvent(eventId, window, objectId, childId);
        }
        catch (Exception exception)
        {
            CoreLogMessages.NativeEventProcessingFailed(
                monitor._logger,
                eventId,
                exception
            );
        }
    }

    private void HandleWinEvent(uint eventId, HWND window, int objectId, int childId)
    {
        if (eventId is EventSystemPeekStart or EventSystemPeekEnd)
        {
            var active = eventId == EventSystemPeekStart;
            CoreLogMessages.AeroPeekChanged(_logger, active);
            _observer(
                new TaskbarNativeEvent(
                    TaskbarNativeEventKind.PeekChanged,
                    IsActive: active
                )
            );
            return;
        }

        TraceWinEvent(eventId, window, objectId, childId);
        if (objectId != 0 || childId != 0)
        {
            return;
        }

        var observation = eventId switch
        {
            PInvoke.EVENT_OBJECT_SHOW
                or PInvoke.EVENT_SYSTEM_MINIMIZEEND
                or PInvoke.EVENT_OBJECT_CREATE
                or PInvoke.EVENT_OBJECT_UNCLOAKED =>
                new TaskbarNativeEvent(TaskbarNativeEventKind.WindowUpserted, window),
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE =>
                new TaskbarNativeEvent(
                    TaskbarNativeEventKind.WindowLocationChanged,
                    window
                ),
            PInvoke.EVENT_OBJECT_HIDE
                or PInvoke.EVENT_SYSTEM_MINIMIZESTART
                or PInvoke.EVENT_OBJECT_DESTROY
                or PInvoke.EVENT_OBJECT_CLOAKED =>
                new TaskbarNativeEvent(TaskbarNativeEventKind.WindowRemoved, window),
            PInvoke.EVENT_SYSTEM_FOREGROUND =>
                new TaskbarNativeEvent(TaskbarNativeEventKind.ForegroundChanged, window),
            _ => default
        };

        if (observation.Kind != TaskbarNativeEventKind.None)
        {
            _observer(observation);
        }
    }

    private void TraceWinEvent(uint eventId, HWND window, int objectId, int childId)
    {
        if (
            !_logger.IsEnabled(LogLevel.Trace)
            || window.IsNull
            || objectId != 0
            || childId != 0
        )
        {
            return;
        }

        CoreLogMessages.WinEventReceived(
            _logger,
            GetEventSource(eventId),
            eventId,
            window,
            objectId,
            childId
        );
    }

    private static string GetEventSource(uint eventId) =>
        eventId switch
        {
            PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_HIDE => "ShowHide",
            PInvoke.EVENT_SYSTEM_MINIMIZESTART or PInvoke.EVENT_SYSTEM_MINIMIZEEND =>
                "MinimizeRestore",
            PInvoke.EVENT_OBJECT_LOCATIONCHANGE => "LocationChange",
            PInvoke.EVENT_SYSTEM_FOREGROUND => "Foreground",
            PInvoke.EVENT_OBJECT_CREATE or PInvoke.EVENT_OBJECT_DESTROY => "CreateDestroy",
            PInvoke.EVENT_OBJECT_CLOAKED or PInvoke.EVENT_OBJECT_UNCLOAKED =>
                "CloakUncloak",
            _ => "Unknown"
        };
}