using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable S6640 // Win32 interop requires unsafe buffers and pointers.

namespace ImmersiveTB.Core.Services;

/// <summary>
///     Identifies the algorithm used to reduce captured pixels to one color.
/// </summary>
public enum ColorSamplingAlgorithm
{
    /// <summary>
    ///     Selects the most frequent quantized color bucket.
    /// </summary>
    DominantColor,

    /// <summary>
    ///     Samples the center of a Gaussian-filtered region.
    /// </summary>
    GaussianBlur
}

/// <summary>
///     Produces final colors while owning capture selection, fallback, region discovery,
///     frame lifetime, and pixel processing.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class ColorSamplerService : IColorSamplerService
{
    private const int GraphicsCaptureRetryCooldownMilliseconds = 30_000;
    private const string TaskbarClassName = "Shell_TrayWnd";
    private readonly Dictionary<ScreenCaptureMethod, IScreenCapture> _captures;
    private readonly ILogger<ColorSamplerService> _logger;
    private readonly IOptionsMonitor<PostProcessOptions> _postProcessOptions;
    private readonly IOptionsMonitor<ColorSamplingOptions> _samplingOptions;
    private int _graphicsCaptureDisabled;
    private long _graphicsCaptureRetryAfter;

    /// <summary>
    ///     Initializes the sampler with every registered capture adapter and its runtime options.
    /// </summary>
    public ColorSamplerService(
        IEnumerable<IScreenCapture> captures,
        IOptionsMonitor<ColorSamplingOptions> samplingOptions,
        IOptionsMonitor<PostProcessOptions> postProcessOptions,
        ILogger<ColorSamplerService> logger
    )
    {
        _captures = captures.ToDictionary(capture => capture.Method);
        _samplingOptions = samplingOptions;
        _postProcessOptions = postProcessOptions;
        _logger = logger;
        CoreLogMessages.ColorSamplerStarted(_logger, _captures.Count);
    }

    /// <inheritdoc />
    public async Task<Color> SampleAsync(CancellationToken cancellationToken = default)
    {
        var sampleOptions = Snapshot(_samplingOptions.CurrentValue);
        var postProcessOptions = Snapshot(_postProcessOptions.CurrentValue);

        try
        {
            var captureMethod = SelectCaptureMethod(sampleOptions.CaptureMethod);
            if (!_captures.TryGetValue(captureMethod, out var capture))
            {
                throw new InvalidOperationException(
                    $"Capture method '{captureMethod}' is not registered."
                );
            }

            var region = GetSampleRegion(sampleOptions);
            CoreLogMessages.ColorSamplingStarted(
                _logger,
                captureMethod,
                region.X,
                region.Y,
                region.Width,
                region.Height
            );
            using var frame = await CaptureWithFallbackAsync(
                    capture,
                    captureMethod,
                    region,
                    cancellationToken
                )
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var color = ColorProcessor.Sample(frame, sampleOptions);
            return postProcessOptions.Enabled
                ? ColorProcessor.ApplyPostProcess(color, postProcessOptions)
                : color;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            CoreLogMessages.ColorSamplingFailed(
                _logger,
                sampleOptions.CaptureMethod,
                exception
            );
            throw;
        }
    }

    private ScreenCaptureMethod SelectCaptureMethod(ScreenCaptureMethod requestedMethod)
    {
        if (
            requestedMethod == ScreenCaptureMethod.WindowsGraphicsCapture
            && _captures.ContainsKey(ScreenCaptureMethod.Gdi)
        )
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
            {
                CoreLogMessages.CaptureFallbackSelected(_logger);
                return ScreenCaptureMethod.Gdi;
            }

            if (
                Volatile.Read(ref _graphicsCaptureDisabled) != 0
                || Environment.TickCount64
                < Interlocked.Read(ref _graphicsCaptureRetryAfter)
            )
            {
                return ScreenCaptureMethod.Gdi;
            }
        }

        return requestedMethod;
    }

    private async ValueTask<CapturedFrame> CaptureWithFallbackAsync(
        IScreenCapture capture,
        ScreenCaptureMethod captureMethod,
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await capture.CaptureAsync(region, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException
            && captureMethod != ScreenCaptureMethod.Gdi
            && _captures.TryGetValue(ScreenCaptureMethod.Gdi, out _)
        )
        {
            RecordCaptureFailure(captureMethod, exception);
            CoreLogMessages.CaptureFailedOverToGdi(_logger, captureMethod, exception);
            return await _captures[ScreenCaptureMethod.Gdi]
                .CaptureAsync(region, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private void RecordCaptureFailure(
        ScreenCaptureMethod captureMethod,
        Exception exception
    )
    {
        if (captureMethod != ScreenCaptureMethod.WindowsGraphicsCapture)
        {
            return;
        }

        if (
            exception is UnauthorizedAccessException or PlatformNotSupportedException
            || exception.HResult == unchecked((int)0x80070005)
        )
        {
            Volatile.Write(ref _graphicsCaptureDisabled, 1);
            return;
        }

        Interlocked.Exchange(
            ref _graphicsCaptureRetryAfter,
            Environment.TickCount64 + GraphicsCaptureRetryCooldownMilliseconds
        );
    }

    private static CaptureRegion GetSampleRegion(ColorSamplingRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SampleHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(request.TaskbarOffset);

        var bounds = GetTaskbarBounds()
                     ?? throw new InvalidOperationException("The primary taskbar was not found.");
        var taskbar = bounds.Taskbar;
        var monitor = bounds.Monitor;
        var taskbarWidth = taskbar.right - taskbar.left;
        var taskbarHeight = taskbar.bottom - taskbar.top;
        if (taskbarWidth <= 0 || taskbarHeight <= 0)
        {
            throw new InvalidOperationException("The primary taskbar has invalid bounds.");
        }

        var dock = GetTaskbarDock(taskbar, monitor, taskbarWidth >= taskbarHeight);
        var region = CreateSampleRegion(taskbar, monitor, dock, request);
        ValidateSampleRegion(region, monitor);
        return region;
    }

    private static TaskbarDock GetTaskbarDock(
        RECT taskbar,
        RECT monitor,
        bool horizontal
    )
    {
        if (horizontal)
        {
            return Math.Abs(taskbar.top - monitor.top)
                   <= Math.Abs(monitor.bottom - taskbar.bottom)
                ? TaskbarDock.Top
                : TaskbarDock.Bottom;
        }

        return Math.Abs(taskbar.left - monitor.left)
               <= Math.Abs(monitor.right - taskbar.right)
            ? TaskbarDock.Left
            : TaskbarDock.Right;
    }

    private static CaptureRegion CreateSampleRegion(
        RECT taskbar,
        RECT monitor,
        TaskbarDock dock,
        ColorSamplingRequest request
    )
    {
        var horizontalLeft = Math.Max(taskbar.left, monitor.left);
        var horizontalWidth = Math.Min(taskbar.right, monitor.right) - horizontalLeft;
        var verticalTop = Math.Max(taskbar.top, monitor.top);
        var verticalHeight = Math.Min(taskbar.bottom, monitor.bottom) - verticalTop;

        return dock switch
        {
            TaskbarDock.Top => new CaptureRegion(
                horizontalLeft,
                taskbar.bottom + request.TaskbarOffset,
                horizontalWidth,
                request.SampleHeight
            ),
            TaskbarDock.Bottom => new CaptureRegion(
                horizontalLeft,
                taskbar.top - request.TaskbarOffset - request.SampleHeight,
                horizontalWidth,
                request.SampleHeight
            ),
            TaskbarDock.Left => new CaptureRegion(
                taskbar.right + request.TaskbarOffset,
                verticalTop,
                request.SampleHeight,
                verticalHeight
            ),
            TaskbarDock.Right => new CaptureRegion(
                taskbar.left - request.TaskbarOffset - request.SampleHeight,
                verticalTop,
                request.SampleHeight,
                verticalHeight
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(dock), dock, null)
        };
    }

    private static void ValidateSampleRegion(CaptureRegion region, RECT monitor)
    {
        if (
            region.Width <= 0
            || region.Height <= 0
            || region.X < monitor.left
            || region.Y < monitor.top
            || (long)region.X + region.Width > monitor.right
            || (long)region.Y + region.Height > monitor.bottom
        )
        {
            throw new InvalidOperationException(
                "The taskbar sampling region falls outside its display."
            );
        }
    }

    private static ColorSamplingRequest Snapshot(ColorSamplingOptions options) =>
        new(
            options.CaptureMethod,
            options.SampleHeight,
            options.TaskbarOffset,
            options.Algorithm,
            options.GaussianBlurOptions.Radius
        );

    private static ColorPostProcessRequest Snapshot(PostProcessOptions options) =>
        new(
            options.Enabled,
            options.BrightnessAdjustment,
            options.SaturationAdjustment,
            options.ContrastAdjustment,
            options.Opacity
        );

    private static TaskbarBounds? GetTaskbarBounds()
    {
        unsafe
        {
            fixed (char* className = TaskbarClassName)
            {
                var window = PInvoke.FindWindow(className, null);
                if (window == HWND.Null)
                {
                    return null;
                }

                if (!PInvoke.GetWindowRect(window, out var taskbar))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var monitor = PInvoke.MonitorFromWindow(
                    window,
                    MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST
                );
                if (monitor == default)
                {
                    throw new InvalidOperationException(
                        "The primary taskbar display was not found."
                    );
                }

                var monitorInfo = new MONITORINFO
                {
                    cbSize = (uint)sizeof(MONITORINFO)
                };
                if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return new TaskbarBounds(taskbar, monitorInfo.rcMonitor);
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TaskbarBounds(RECT Taskbar, RECT Monitor);

    private enum TaskbarDock
    {
        Left,
        Top,
        Right,
        Bottom
    }
}