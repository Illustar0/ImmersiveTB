using ImmersiveTB.Core.Models;

namespace ImmersiveTB.Core.Contracts.Services;

/// <summary>
///     Captures a physical desktop region as BGRA8 pixels.
/// </summary>
public interface IScreenCapture
{
    /// <summary>
    ///     Gets the implementation identifier used by sampling options.
    /// </summary>
    ScreenCaptureMethod Method
    {
        get;
    }

    /// <summary>
    ///     Captures the requested desktop region.
    /// </summary>
    ValueTask<CapturedFrame> CaptureAsync(
        CaptureRegion region,
        CancellationToken cancellationToken
    );
}