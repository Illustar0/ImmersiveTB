using System.Runtime.InteropServices.Marshalling;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Models;
using ImmersiveTB.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using WinRT;
using static Vortice.Direct3D11.D3D11;
using DisplayId = Windows.Graphics.DisplayId;
using IInspectable = Windows.Win32.System.WinRT.IInspectable;
using MapFlags = Vortice.Direct3D11.MapFlags;
using SharpGenComObject = SharpGen.Runtime.ComObject;

#pragma warning disable S6640 // WGC interop requires unsafe GPU buffer access.
#pragma warning disable MA0051 // Capture keeps resource mapping and cleanup together.

namespace ImmersiveTB.Adapters.ScreenCapture;

/// <summary>
///     Captures the primary display through Windows Graphics Capture.
/// </summary>
public sealed class GraphicsCaptureScreenCapture : IScreenCapture, IDisposable
{
    private readonly Lazy<Task<int>> _borderlessAccess;
    private readonly Lazy<IDirect3DDevice> _device = new(CreateDirect3DDevice);
    private readonly ILogger<GraphicsCaptureScreenCapture> _logger;
    private readonly Lazy<Task<int>> _programmaticAccess;
    private GraphicsCaptureItem? _captureItem;
    private DisplayId _captureItemDisplayId;

    /// <summary>
    ///     Initializes a Windows Graphics Capture adapter.
    /// </summary>
    public GraphicsCaptureScreenCapture(ILogger<GraphicsCaptureScreenCapture> logger)
    {
        _logger = logger;
        _programmaticAccess = new Lazy<Task<int>>(() => RequestAccessAsync(GraphicsCaptureAccessKind.Programmatic)
        );
        _borderlessAccess = new Lazy<Task<int>>(() => RequestAccessAsync(GraphicsCaptureAccessKind.Borderless)
        );
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _captureItem = null;
        if (_device.IsValueCreated)
        {
            _device.Value.Dispose();
        }
    }

    /// <inheritdoc />
    public ScreenCaptureMethod Method => ScreenCaptureMethod.WindowsGraphicsCapture;

    /// <inheritdoc />
    public async ValueTask<CapturedFrame> CaptureAsync(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            throw new PlatformNotSupportedException(
                "Programmatic Windows Graphics Capture requires Windows build 20348 or later."
            );
        }

        var access = await _programmaticAccess.Value.ConfigureAwait(false);
        if (access != (int)AppCapabilityAccessStatus.Allowed)
        {
            throw new UnauthorizedAccessException(
                $"Programmatic graphics capture access was not granted: "
                + $"{(AppCapabilityAccessStatus)access}."
            );
        }

        var item = GetCaptureItem();
        try
        {
            using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _device.Value,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                item.Size
            );
            using var session = framePool.CreateCaptureSession(item);
            var borderlessAccess = await _borderlessAccess.Value.ConfigureAwait(false);
            if (borderlessAccess == (int)AppCapabilityAccessStatus.Allowed)
            {
                session.IsBorderRequired = false;
            }

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                session.IsCursorCaptureEnabled = false;
            }

            var frameSource = new TaskCompletionSource<Direct3D11CaptureFrame>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
            {
                var frame = sender.TryGetNextFrame();
                if (!frameSource.TrySetResult(frame))
                {
                    frame.Dispose();
                }
            }

            framePool.FrameArrived += OnFrameArrived;
            using var cancellation = cancellationToken.Register(
                state => ((TaskCompletionSource<Direct3D11CaptureFrame>)state!).TrySetCanceled(
                    cancellationToken
                ),
                frameSource
            );

            try
            {
                session.StartCapture();
                using var captureFrame = await frameSource.Task
                    .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken)
                    .ConfigureAwait(false);
                return CopyRegion(captureFrame.Surface, region);
            }
            finally
            {
                framePool.FrameArrived -= OnFrameArrived;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (ReferenceEquals(_captureItem, item))
            {
                _captureItem = null;
            }

            throw;
        }
    }

    private GraphicsCaptureItem GetCaptureItem()
    {
        var monitor = PInvoke.MonitorFromWindow(
            default,
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY
        );
        var displayId = Win32Interop.GetDisplayIdFromMonitor(monitor);
        var captureDisplayId = new DisplayId(displayId.Value);
        if (
            _captureItem is not null
            && _captureItemDisplayId.Value == captureDisplayId.Value
        )
        {
            return _captureItem;
        }

        _captureItem = GraphicsCaptureItem.TryCreateFromDisplayId(captureDisplayId)
                       ?? throw new InvalidOperationException(
                           "Windows Graphics Capture cannot access the primary display."
                       );
        _captureItemDisplayId = captureDisplayId;
        return _captureItem;
    }

    private async Task<int> RequestAccessAsync(GraphicsCaptureAccessKind accessKind)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348))
        {
            throw new PlatformNotSupportedException(
                "Programmatic Windows Graphics Capture requires Windows build 20348 or later."
            );
        }

        var access = await GraphicsCaptureAccess
            .RequestAccessAsync(accessKind)
            .AsTask(CancellationToken.None)
            .ConfigureAwait(false);
        AppLogMessages.GraphicsCaptureAccessResolved(_logger, accessKind, access);
        return (int)access;
    }

    private static unsafe CapturedFrame CopyRegion(
        IDirect3DSurface surface,
        CaptureRegion region
    )
    {
        using var sourceTexture = GetTexture(surface);
        var sourceDescription = sourceTexture.Description;
        if (region.X < 0 || region.Y < 0 ||
            region.X + region.Width > sourceDescription.Width ||
            region.Y + region.Height > sourceDescription.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                "The capture region must be inside the primary display."
            );
        }

        using var device = sourceTexture.Device;
        using var context = device.ImmediateContext;
        using var stagingTexture = CreateStagingTexture(
            device,
            sourceDescription.Format,
            region.Width,
            region.Height
        );
        context.CopySubresourceRegion(
            stagingTexture,
            0,
            0,
            0,
            0,
            sourceTexture,
            0,
            new Box(
                region.X,
                region.Y,
                0,
                region.X + region.Width,
                region.Y + region.Height,
                1
            )
        );
        context.Map(
            stagingTexture,
            0,
            MapMode.Read,
            MapFlags.None,
            out var mappedResource
        ).CheckError();

        CapturedFrame? frame = null;
        try
        {
            frame = new CapturedFrame(region.Width, region.Height);
            var destination = frame.Pixels.Span;
            var rowLength = region.Width * 4;
            var source = (byte*)mappedResource.DataPointer;
            if (mappedResource.RowPitch == rowLength)
            {
                new ReadOnlySpan<byte>(source, checked(rowLength * region.Height))
                    .CopyTo(destination);
                return frame;
            }

            for (var row = 0; row < region.Height; row++)
            {
                new ReadOnlySpan<byte>(source + row * mappedResource.RowPitch, rowLength)
                    .CopyTo(destination.Slice(row * rowLength, rowLength));
            }
        }
        catch
        {
            frame?.Dispose();
            throw;
        }
        finally
        {
            context.Unmap(stagingTexture, 0);
        }

        return frame;
    }

    private static ID3D11Texture2D CreateStagingTexture(
        ID3D11Device device,
        Format format,
        int width,
        int height
    ) =>
        device.CreateTexture2D(
            new Texture2DDescription(
                format,
                (uint)width,
                (uint)height,
                1,
                1,
                BindFlags.None,
                ResourceUsage.Staging,
                CpuAccessFlags.Read
            )
        );

    private static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var surfacePointer = MarshalInterface<IDirect3DSurface>.FromManaged(surface);
        using var surfaceAccess = SharpGenComObject.As<IDirect3DDxgiInterfaceAccess>(
            surfacePointer
        );
        return surfaceAccess.GetInterface<ID3D11Texture2D>();
    }

    private static unsafe IDirect3DDevice CreateDirect3DDevice()
    {
        using var d3dDevice = D3D11CreateDevice(
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport
        );
        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        var nativeDxgiDevice = ComInterfaceMarshaller<
            Windows.Win32.Graphics.Dxgi.IDXGIDevice
        >.ConvertToManaged((void*)dxgiDevice.NativePointer);
        PInvoke.CreateDirect3D11DeviceFromDXGIDevice(
            nativeDxgiDevice,
            out var inspectable
        ).ThrowOnFailure();
        var inspectablePointer = ComInterfaceMarshaller<
            IInspectable
        >.ConvertToUnmanaged(inspectable);
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi((nint)inspectablePointer);
        }
        finally
        {
            ComInterfaceMarshaller<IInspectable>.Free(
                inspectablePointer
            );
        }
    }
}