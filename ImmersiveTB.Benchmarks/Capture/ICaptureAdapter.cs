using ImmersiveTB.Core.Models;

namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Provides one benchmark-owned screen-capture strategy.
/// </summary>
internal interface ICaptureAdapter : IDisposable
{
    /// <summary>
    ///     Captures a physical desktop region as BGRA8 pixels.
    /// </summary>
    ValueTask<CaptureBuffer> CaptureAsync(
        CaptureRegion region,
        CancellationToken cancellationToken
    );
}