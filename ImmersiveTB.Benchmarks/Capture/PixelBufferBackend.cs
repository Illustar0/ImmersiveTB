namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Selects the pooled-memory implementation used for captured pixels.
/// </summary>
public enum PixelBufferBackend
{
    /// <summary>Uses DotNext.Buffers.MemoryOwner over ArrayPool.Shared.</summary>
    DotNext,

    /// <summary>Uses CommunityToolkit.HighPerformance.Buffers.MemoryOwner.</summary>
    CommunityToolkit
}