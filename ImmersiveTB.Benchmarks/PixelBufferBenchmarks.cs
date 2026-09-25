using BenchmarkDotNet.Attributes;
using ImmersiveTB.Benchmarks.Capture;

namespace ImmersiveTB.Benchmarks;

/// <summary>
///     Isolates pooled-owner allocation and a 10-pixel taskbar-strip copy.
/// </summary>
[Config(typeof(PixelBufferBenchmarkConfig))]
public class PixelBufferBenchmarks
{
    private const int Height = 10;
    private const int Width = 1920;
    private byte[] _source = null!;

    /// <summary>Creates deterministic BGRA8 source pixels.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _source = GC.AllocateUninitializedArray<byte>(Width * Height * 4);
        Random.Shared.NextBytes(_source);
    }

    /// <summary>Measures DotNext pooled allocation, copy, and return.</summary>
    [Benchmark(Baseline = true, Description = "Buffer: DotNext")]
    public int DotNext()
    {
        using var buffer = CaptureBuffer.Allocate(
            Width,
            Height,
            PixelBufferBackend.DotNext
        );
        _source.CopyTo(buffer.Memory);
        return buffer.Memory.Span[0];
    }

    /// <summary>Measures CommunityToolkit pooled allocation, copy, and return.</summary>
    [Benchmark(Description = "Buffer: CommunityToolkit.HighPerformance")]
    public int CommunityToolkit()
    {
        using var buffer = CaptureBuffer.Allocate(
            Width,
            Height,
            PixelBufferBackend.CommunityToolkit
        );
        _source.CopyTo(buffer.Memory);
        return buffer.Memory.Span[0];
    }
}