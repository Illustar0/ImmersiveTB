using BenchmarkDotNet.Attributes;
using ImmersiveTB.Benchmarks.Capture;
using ImmersiveTB.Core.Models;
using System.Diagnostics.CodeAnalysis;
using Windows.Win32;
using Windows.Win32.Foundation;

#pragma warning disable S6640 // Benchmark adapters exercise native capture interop.

namespace ImmersiveTB.Benchmarks;

/// <summary>
///     Compares real taskbar-strip capture latency and allocation across WGC and GDI strategies.
/// </summary>
[Config(typeof(CaptureBenchmarkConfig))]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet invokes GlobalCleanup after each benchmark case."
)]
public class ScreenCaptureBenchmarks
{
    private const int SampleHeight = 10;
    private GdiCaptureAdapter _gdiAllCaches = null!;
    private GdiCaptureAdapter _gdiDeviceContextCache = null!;
    private GdiCaptureAdapter _gdiDibSection = null!;
    private GdiCaptureAdapter _gdiNoCache = null!;
    private CaptureRegion _region;
    private WgcCaptureAdapter _wgcAllCaches = null!;
    private WgcCaptureAdapter _wgcCaptureItemCache = null!;
    private WgcCaptureAdapter _wgcNoResourceCache = null!;
    private WgcCaptureAdapter _wgcStagingCache = null!;

    /// <summary>Gets or sets the pooled pixel-buffer implementation.</summary>
    [Params(
        PixelBufferBackend.DotNext,
        PixelBufferBackend.CommunityToolkit
    )]
    public PixelBufferBackend BufferBackend
    {
        get;
        set;
    }

    /// <summary>
    ///     Creates independent adapters and validates that every strategy can capture.
    /// </summary>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _region = GetTaskbarSampleRegion();
        _gdiNoCache = new GdiCaptureAdapter(GdiCacheMode.None, BufferBackend);
        _gdiDeviceContextCache = new GdiCaptureAdapter(
            GdiCacheMode.DeviceContexts,
            BufferBackend
        );
        _gdiAllCaches = new GdiCaptureAdapter(GdiCacheMode.All, BufferBackend);
        _gdiDibSection = new GdiCaptureAdapter(
            GdiCacheMode.None,
            BufferBackend,
            true
        );
        _wgcNoResourceCache = new WgcCaptureAdapter(
            WgcCacheMode.None,
            BufferBackend
        );
        _wgcCaptureItemCache = new WgcCaptureAdapter(
            WgcCacheMode.CaptureItem,
            BufferBackend
        );
        _wgcStagingCache = new WgcCaptureAdapter(
            WgcCacheMode.StagingTexture,
            BufferBackend
        );
        _wgcAllCaches = new WgcCaptureAdapter(WgcCacheMode.All, BufferBackend);

        await ValidateAsync(_gdiNoCache).ConfigureAwait(false);
        await ValidateAsync(_gdiDeviceContextCache).ConfigureAwait(false);
        await ValidateAsync(_gdiAllCaches).ConfigureAwait(false);
        await ValidateAsync(_gdiDibSection).ConfigureAwait(false);
        await ValidateAsync(_wgcNoResourceCache).ConfigureAwait(false);
        await ValidateAsync(_wgcCaptureItemCache).ConfigureAwait(false);
        await ValidateAsync(_wgcStagingCache).ConfigureAwait(false);
        await ValidateAsync(_wgcAllCaches).ConfigureAwait(false);
    }

    /// <summary>
    ///     Releases cached native and GPU resources after each benchmark case.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _wgcAllCaches.Dispose();
        _wgcStagingCache.Dispose();
        _wgcCaptureItemCache.Dispose();
        _wgcNoResourceCache.Dispose();
        _gdiAllCaches.Dispose();
        _gdiDibSection.Dispose();
        _gdiDeviceContextCache.Dispose();
        _gdiNoCache.Dispose();
    }

    /// <summary>Measures the production-style GDI path that recreates native resources.</summary>
    [Benchmark(Baseline = true, Description = "GDI: recreate DC and bitmap")]
    public ValueTask<int> GdiNoCache() => CaptureChecksumAsync(_gdiNoCache);

    /// <summary>Measures a candidate GDI path that reuses device contexts.</summary>
    [Benchmark(Description = "GDI: reuse screen + memory DC")]
    public ValueTask<int> GdiDeviceContextCache() =>
        CaptureChecksumAsync(_gdiDeviceContextCache);

    /// <summary>Measures a candidate GDI path that also reuses the bitmap.</summary>
    [Benchmark(Description = "GDI: reuse DC + bitmap")]
    public ValueTask<int> GdiAllCaches() => CaptureChecksumAsync(_gdiAllCaches);

    /// <summary>Measures direct DIB-section pixels without a GetDIBits conversion.</summary>
    [Benchmark(Description = "GDI: direct DIB section")]
    public ValueTask<int> GdiDibSection() => CaptureChecksumAsync(_gdiDibSection);

    /// <summary>Measures WGC with per-sample capture item and staging texture creation.</summary>
    [Benchmark(Description = "WGC: no item/staging cache")]
    public ValueTask<int> WgcNoResourceCache() =>
        CaptureChecksumAsync(_wgcNoResourceCache);

    /// <summary>Measures WGC while reusing only the primary-display capture item.</summary>
    [Benchmark(Description = "WGC: capture-item cache")]
    public ValueTask<int> WgcCaptureItemCache() =>
        CaptureChecksumAsync(_wgcCaptureItemCache);

    /// <summary>Measures WGC while reusing only the readback staging texture.</summary>
    [Benchmark(Description = "WGC: staging-texture cache")]
    public ValueTask<int> WgcStagingCache() => CaptureChecksumAsync(_wgcStagingCache);

    /// <summary>Measures WGC with both production-safe resource caches.</summary>
    [Benchmark(Description = "WGC: item + staging caches")]
    public ValueTask<int> WgcAllCaches() => CaptureChecksumAsync(_wgcAllCaches);

    private async ValueTask<int> CaptureChecksumAsync(ICaptureAdapter adapter)
    {
        using var frame = await adapter
            .CaptureAsync(_region, CancellationToken.None)
            .ConfigureAwait(false);
        return frame.Memory.Span[0];
    }

    private async Task ValidateAsync(ICaptureAdapter adapter)
    {
        using var frame = await adapter
            .CaptureAsync(_region, CancellationToken.None)
            .ConfigureAwait(false);
        if (frame.Width != _region.Width || frame.Height != _region.Height)
        {
            throw new InvalidOperationException("The capture adapter returned an invalid frame size.");
        }
    }

    private static unsafe CaptureRegion GetTaskbarSampleRegion()
    {
        fixed (char* className = "Shell_TrayWnd")
        {
            var taskbar = PInvoke.FindWindow(className, null);
            if (taskbar == HWND.Null || !PInvoke.GetWindowRect(taskbar, out var rect))
            {
                throw new InvalidOperationException("The primary taskbar was not found.");
            }

            var width = rect.right - rect.left;
            if (width <= 0)
            {
                throw new InvalidOperationException("The primary taskbar has an invalid width.");
            }

            return new CaptureRegion(
                rect.left,
                Math.Max(0, rect.top - SampleHeight),
                width,
                SampleHeight
            );
        }
    }
}