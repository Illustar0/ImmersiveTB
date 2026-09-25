using System.Runtime.Versioning;
using ImmersiveTB.Core.Models;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

#pragma warning disable S6640 // GDI interop requires unsafe buffers and pointers.
#pragma warning disable MA0051 // Capture keeps native resource ownership in one flow.

namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Captures through GDI with either per-call or reusable native resources.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
internal sealed class GdiCaptureAdapter(
    GdiCacheMode cacheMode,
    PixelBufferBackend bufferBackend,
    bool useDibSection = false
) : ICaptureAdapter
{
    private HBITMAP _bitmap;
    private int _height;
    private HDC _memoryDc;
    private HGDIOBJ _previousBitmap;
    private HDC _screenDc;
    private int _width;

    /// <inheritdoc />
    public void Dispose() => ReleaseReusableResources();

    /// <inheritdoc />
    public unsafe ValueTask<CaptureBuffer> CaptureAsync(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Height);
        cancellationToken.ThrowIfCancellationRequested();

        if ((cacheMode & GdiCacheMode.DeviceContexts) == GdiCacheMode.None)
        {
            return useDibSection
                ? CaptureWithFreshDibSection(region, cancellationToken)
                : CaptureWithFreshResources(region, cancellationToken);
        }

        return (cacheMode & GdiCacheMode.Bitmap) != GdiCacheMode.None
            ? CaptureWithReusableResources(region, cancellationToken)
            : CaptureWithReusableDeviceContexts(region, cancellationToken);
    }

    private unsafe ValueTask<CaptureBuffer> CaptureWithFreshDibSection(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        HDC screenDc = default;
        HDC memoryDc = default;
        HBITMAP bitmap = default;
        HGDIOBJ previousBitmap = default;

        try
        {
            screenDc = PInvoke.GetDC(HWND.Null);
            if (screenDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to acquire the desktop DC.");
            }

            memoryDc = PInvoke.CreateCompatibleDC(screenDc);
            if (memoryDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to create a compatible DC.");
            }

            var bitmapInfo = CreateBitmapInfo(region.Width, region.Height);
            void* pixels;
            bitmap = PInvoke.CreateDIBSection(
                screenDc,
                &bitmapInfo,
                DIB_USAGE.DIB_RGB_COLORS,
                &pixels,
                default,
                0
            );
            if (bitmap == HBITMAP.Null || pixels is null)
            {
                throw new InvalidOperationException("GDI failed to create a DIB section.");
            }

            previousBitmap = PInvoke.SelectObject(memoryDc, bitmap);
            if (previousBitmap == HGDIOBJ.Null)
            {
                throw new InvalidOperationException("GDI failed to select the DIB section.");
            }

            if (!PInvoke.BitBlt(
                    memoryDc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDc,
                    region.X,
                    region.Y,
                    ROP_CODE.SRCCOPY
                ) || !PInvoke.GdiFlush())
            {
                throw new InvalidOperationException("GDI failed to capture into the DIB section.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var frame = CaptureBuffer.Allocate(
                region.Width,
                region.Height,
                bufferBackend
            );
            try
            {
                new ReadOnlySpan<byte>(
                    pixels,
                    checked(region.Width * region.Height * 4)
                ).CopyTo(frame.Memory.Span);
                return ValueTask.FromResult(frame);
            }
            catch
            {
                frame.Dispose();
                throw;
            }
        }
        finally
        {
            ReleaseResources(screenDc, memoryDc, bitmap, previousBitmap);
        }
    }

    private unsafe ValueTask<CaptureBuffer> CaptureWithFreshResources(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        HDC screenDc = default;
        HDC memoryDc = default;
        HBITMAP bitmap = default;
        HGDIOBJ previousBitmap = default;

        try
        {
            screenDc = PInvoke.GetDC(HWND.Null);
            if (screenDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to acquire the desktop DC.");
            }

            memoryDc = PInvoke.CreateCompatibleDC(screenDc);
            if (memoryDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to create a compatible DC.");
            }

            bitmap = PInvoke.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == HBITMAP.Null)
            {
                throw new InvalidOperationException("GDI failed to create a capture bitmap.");
            }

            previousBitmap = PInvoke.SelectObject(memoryDc, bitmap);
            if (previousBitmap == HGDIOBJ.Null)
            {
                throw new InvalidOperationException("GDI failed to select the capture bitmap.");
            }

            return ValueTask.FromResult(
                CapturePixels(screenDc, memoryDc, bitmap, region, cancellationToken)
            );
        }
        finally
        {
            ReleaseResources(screenDc, memoryDc, bitmap, previousBitmap);
        }
    }

    private unsafe ValueTask<CaptureBuffer> CaptureWithReusableResources(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        EnsureReusableResources(region.Width, region.Height);
        return ValueTask.FromResult(
            CapturePixels(_screenDc, _memoryDc, _bitmap, region, cancellationToken)
        );
    }

    private unsafe ValueTask<CaptureBuffer> CaptureWithReusableDeviceContexts(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        EnsureReusableDeviceContexts();
        var bitmap = PInvoke.CreateCompatibleBitmap(
            _screenDc,
            region.Width,
            region.Height
        );
        if (bitmap == HBITMAP.Null)
        {
            throw new InvalidOperationException("GDI failed to create a capture bitmap.");
        }

        var previousBitmap = PInvoke.SelectObject(_memoryDc, bitmap);
        if (previousBitmap == HGDIOBJ.Null)
        {
            PInvoke.DeleteObject(bitmap);
            throw new InvalidOperationException("GDI failed to select the capture bitmap.");
        }

        try
        {
            return ValueTask.FromResult(
                CapturePixels(
                    _screenDc,
                    _memoryDc,
                    bitmap,
                    region,
                    cancellationToken
                )
            );
        }
        finally
        {
            PInvoke.SelectObject(_memoryDc, previousBitmap);
            PInvoke.DeleteObject(bitmap);
        }
    }

    private void EnsureReusableResources(int width, int height)
    {
        if (_screenDc != HDC.Null && _width == width && _height == height)
        {
            return;
        }

        ReleaseReusableResources();
        _screenDc = PInvoke.GetDC(HWND.Null);
        if (_screenDc == HDC.Null)
        {
            throw new InvalidOperationException("GDI failed to acquire the desktop DC.");
        }

        try
        {
            _memoryDc = PInvoke.CreateCompatibleDC(_screenDc);
            if (_memoryDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to create a compatible DC.");
            }

            _bitmap = PInvoke.CreateCompatibleBitmap(_screenDc, width, height);
            if (_bitmap == HBITMAP.Null)
            {
                throw new InvalidOperationException("GDI failed to create a capture bitmap.");
            }

            _previousBitmap = PInvoke.SelectObject(_memoryDc, _bitmap);
            if (_previousBitmap == HGDIOBJ.Null)
            {
                throw new InvalidOperationException("GDI failed to select the capture bitmap.");
            }

            _width = width;
            _height = height;
        }
        catch
        {
            ReleaseReusableResources();
            throw;
        }
    }

    private void EnsureReusableDeviceContexts()
    {
        if (_screenDc != HDC.Null)
        {
            return;
        }

        _screenDc = PInvoke.GetDC(HWND.Null);
        if (_screenDc == HDC.Null)
        {
            throw new InvalidOperationException("GDI failed to acquire the desktop DC.");
        }

        _memoryDc = PInvoke.CreateCompatibleDC(_screenDc);
        if (_memoryDc != HDC.Null)
        {
            return;
        }

        PInvoke.ReleaseDC(HWND.Null, _screenDc);
        _screenDc = HDC.Null;
        throw new InvalidOperationException("GDI failed to create a compatible DC.");
    }

    private unsafe CaptureBuffer CapturePixels(
        HDC screenDc,
        HDC memoryDc,
        HBITMAP bitmap,
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        if (!PInvoke.BitBlt(
                memoryDc,
                0,
                0,
                region.Width,
                region.Height,
                screenDc,
                region.X,
                region.Y,
                ROP_CODE.SRCCOPY
            ))
        {
            throw new InvalidOperationException("GDI failed to copy the desktop region.");
        }

        var bitmapInfo = CreateBitmapInfo(region.Width, region.Height);

        var frame = CaptureBuffer.Allocate(
            region.Width,
            region.Height,
            bufferBackend
        );
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var handle = frame.Memory.Pin();
            var scanLines = PInvoke.GetDIBits(
                memoryDc,
                bitmap,
                0,
                (uint)region.Height,
                handle.Pointer,
                &bitmapInfo,
                DIB_USAGE.DIB_RGB_COLORS
            );
            if (scanLines == 0)
            {
                throw new InvalidOperationException("GDI failed to read the capture bitmap.");
            }

            return frame;
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    private static unsafe BITMAPINFO CreateBitmapInfo(int width, int height) =>
        new()
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0
            }
        };

    private void ReleaseReusableResources()
    {
        ReleaseResources(_screenDc, _memoryDc, _bitmap, _previousBitmap);
        _screenDc = HDC.Null;
        _memoryDc = HDC.Null;
        _bitmap = HBITMAP.Null;
        _previousBitmap = HGDIOBJ.Null;
        _width = 0;
        _height = 0;
    }

    private static void ReleaseResources(
        HDC screenDc,
        HDC memoryDc,
        HBITMAP bitmap,
        HGDIOBJ previousBitmap
    )
    {
        if (previousBitmap != HGDIOBJ.Null && memoryDc != HDC.Null)
        {
            PInvoke.SelectObject(memoryDc, previousBitmap);
        }

        if (bitmap != HBITMAP.Null)
        {
            PInvoke.DeleteObject(bitmap);
        }

        if (memoryDc != HDC.Null)
        {
            PInvoke.DeleteDC(memoryDc);
        }

        if (screenDc != HDC.Null)
        {
            PInvoke.ReleaseDC(HWND.Null, screenDc);
        }
    }
}