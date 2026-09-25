namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Selects WGC resources retained between benchmark invocations.
/// </summary>
[Flags]
internal enum WgcCacheMode
{
    /// <summary>Recreates the capture item and staging texture.</summary>
    None = 0,

    /// <summary>Reuses the primary-display capture item.</summary>
    CaptureItem = 1,

    /// <summary>Reuses the GPU readback staging texture.</summary>
    StagingTexture = 2,

    /// <summary>Reuses both production-safe resources.</summary>
    All = CaptureItem | StagingTexture
}