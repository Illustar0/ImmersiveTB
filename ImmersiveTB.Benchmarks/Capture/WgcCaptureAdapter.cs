using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ImmersiveTB.Core.Models;
using SharpGenComObject = SharpGen.Runtime.ComObject;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using WinRT;
using static Vortice.Direct3D11.D3D11;
using IInspectable = Windows.Win32.System.WinRT.IInspectable;
using MapFlags = Vortice.Direct3D11.MapFlags;

#pragma warning disable S6640 // WGC interop requires unsafe GPU buffer access.
#pragma warning disable MA0051 // Capture keeps resource mapping and cleanup together.

namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Runs the ImmersiveTB WGC pipeline with benchmark-owned cache policies.
/// </summary>
internal sealed class WgcCaptureAdapter : ICaptureAdapter
{
    private static readonly Guid GraphicsCaptureItemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private readonly PixelBufferBackend _bufferBackend;
    private readonly WgcCacheMode _cacheMode;
    private readonly IDirect3DDevice _device = CreateDirect3DDevice();
    private GraphicsCaptureItem? _captureItem;
    private nint _captureItemMonitor;
    private Format _stagingFormat;
    private int _stagingHeight;
    private ID3D11Texture2D? _stagingTexture;
    private int _stagingWidth;

    /// <summary>
    ///     Initializes an independent WGC pipeline with the selected caches.
    /// </summary>
    public WgcCaptureAdapter(
        WgcCacheMode cacheMode,
        PixelBufferBackend bufferBackend
    )
    {
        _cacheMode = cacheMode;
        _bufferBackend = bufferBackend;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stagingTexture?.Dispose();
        _stagingTexture = null;
        _captureItem = null;
        _device.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask<CaptureBuffer> CaptureAsync(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        var item = GetCaptureItem();
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            item.Size
        );
        using var session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.IsBorderRequired = false;
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
            state =>
                ((TaskCompletionSource<Direct3D11CaptureFrame>)state!).TrySetCanceled(
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

    private unsafe GraphicsCaptureItem GetCaptureItem()
    {
        var monitor = PInvoke.MonitorFromWindow(
            default,
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY
        );
        var monitorHandle = (nint)monitor.Value;
        var cachedItem = _captureItem;
        if (
            CachesCaptureItem
            && cachedItem is not null
            && _captureItemMonitor == monitorHandle
        )
        {
            return cachedItem;
        }

        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var itemPointer = interop.CreateForMonitor(monitorHandle, GraphicsCaptureItemId);
        GraphicsCaptureItem item;
        try
        {
            item = GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }

        if (CachesCaptureItem)
        {
            _captureItem = item;
            _captureItemMonitor = monitorHandle;
        }

        return item;
    }

    private unsafe CaptureBuffer CopyRegion(
        IDirect3DSurface surface,
        CaptureRegion region
    )
    {
        using var sourceTexture = GetTexture(surface);
        var sourceDescription = sourceTexture.Description;
        if (
            region.X < 0
            || region.Y < 0
            || region.X + region.Width > sourceDescription.Width
            || region.Y + region.Height > sourceDescription.Height
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(region),
                "The capture region must be inside the primary display."
            );
        }

        using var device = sourceTexture.Device;
        using var context = device.ImmediateContext;
        var stagingTexture = GetStagingTexture(
            device,
            sourceDescription.Format,
            region.Width,
            region.Height
        );
        var isMapped = false;
        CaptureBuffer? frame = null;
        try
        {
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
            isMapped = true;

            frame = CaptureBuffer.Allocate(
                region.Width,
                region.Height,
                _bufferBackend
            );
            var destination = frame.Memory.Span;
            var rowLength = region.Width * 4;
            var source = (byte*)mappedResource.DataPointer;
            if (mappedResource.RowPitch == rowLength)
            {
                new ReadOnlySpan<byte>(source, checked(rowLength * region.Height))
                    .CopyTo(destination);
            }
            else
            {
                for (var row = 0; row < region.Height; row++)
                {
                    new ReadOnlySpan<byte>(
                        source + row * mappedResource.RowPitch,
                        rowLength
                    ).CopyTo(destination.Slice(row * rowLength, rowLength));
                }
            }

            var result = frame;
            frame = null;
            return result;
        }
        finally
        {
            frame?.Dispose();
            if (isMapped)
            {
                context.Unmap(stagingTexture, 0);
            }

            if (!CachesStagingTexture)
            {
                stagingTexture.Dispose();
            }
        }
    }

    private ID3D11Texture2D GetStagingTexture(
        ID3D11Device device,
        Format format,
        int width,
        int height
    )
    {
        if (
            CachesStagingTexture
            && _stagingTexture is not null
            && _stagingFormat == format
            && _stagingWidth == width
            && _stagingHeight == height
        )
        {
            return _stagingTexture;
        }

        var texture = device.CreateTexture2D(
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
        if (CachesStagingTexture)
        {
            _stagingTexture?.Dispose();
            _stagingTexture = texture;
            _stagingFormat = format;
            _stagingWidth = width;
            _stagingHeight = height;
        }

        return texture;
    }

    private bool CachesCaptureItem =>
        (_cacheMode & WgcCacheMode.CaptureItem) != WgcCacheMode.None;

    private bool CachesStagingTexture =>
        (_cacheMode & WgcCacheMode.StagingTexture) != WgcCacheMode.None;

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
            ComInterfaceMarshaller<IInspectable>.Free(inspectablePointer);
        }
    }

    /// <summary>Creates capture items for desktop monitor handles.</summary>
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        /// <summary>Creates a capture item for a window handle.</summary>
        nint CreateForWindow(nint window, in Guid iid);

        /// <summary>Creates a capture item for a monitor handle.</summary>
        nint CreateForMonitor(nint monitor, in Guid iid);
    }
}