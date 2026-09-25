using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Models;

#pragma warning disable S6640 // GDI interop requires unsafe buffers and pointers.
#pragma warning disable MA0051 // Capture keeps native resource ownership in one flow.

namespace ImmersiveTB.Adapters.ScreenCapture;

/// <summary>
///     Captures small desktop regions with GDI.
/// </summary>
[SupportedOSPlatform("windows6.0.6000")]
public sealed class GdiScreenCapture : IScreenCapture
{
    /// <inheritdoc />
    public ScreenCaptureMethod Method => ScreenCaptureMethod.Gdi;

    /// <inheritdoc />
    public unsafe ValueTask<CapturedFrame> CaptureAsync(
        CaptureRegion region,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(region.Height);

        HDC screenDc = default;
        HDC memoryDc = default;
        HBITMAP bitmap = default;
        HGDIOBJ previousBitmap = default;
        CapturedFrame? frame = null;

        try
        {
            screenDc = PInvoke.GetDC(HWND.Null);
            if (screenDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to acquire the desktop device context.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            memoryDc = PInvoke.CreateCompatibleDC(screenDc);
            if (memoryDc == HDC.Null)
            {
                throw new InvalidOperationException("GDI failed to create a compatible device context.");
            }

            bitmap = PInvoke.CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            if (bitmap == HBITMAP.Null)
            {
                throw new InvalidOperationException("GDI failed to create the capture bitmap.");
            }

            previousBitmap = PInvoke.SelectObject(memoryDc, bitmap);
            if (previousBitmap == HGDIOBJ.Null)
            {
                throw new InvalidOperationException("GDI failed to select the capture bitmap.");
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
                ))
            {
                throw new InvalidOperationException("GDI failed to copy the desktop region.");
            }

            var bitmapInfo = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)sizeof(BITMAPINFOHEADER),
                    biWidth = region.Width,
                    biHeight = -region.Height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0
                }
            };

            frame = new CapturedFrame(region.Width, region.Height);
            cancellationToken.ThrowIfCancellationRequested();

            using var handle = frame.Pixels.Pin();
            var scanLines = PInvoke.GetDIBits(
                memoryDc,
                bitmap,
                0,
                (uint)region.Height,
                handle.Pointer,
                &bitmapInfo,
                DIB_USAGE.DIB_RGB_COLORS
            );
            if (scanLines != (uint)region.Height)
            {
                throw new InvalidOperationException(
                    $"GDI read {scanLines} of {region.Height} requested scan lines."
                );
            }

            var result = frame;
            frame = null;
            return ValueTask.FromResult(result);
        }
        finally
        {
            frame?.Dispose();
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
}