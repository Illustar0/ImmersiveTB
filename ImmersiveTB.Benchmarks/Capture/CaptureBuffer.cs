using System.Buffers;
using CommunityToolkit.HighPerformance.Buffers;
using DotNextBuffer = DotNext.Buffers.MemoryOwner<byte>;

namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Owns a benchmark-selected pooled BGRA8 pixel buffer.
/// </summary>
internal abstract class CaptureBuffer : IDisposable
{
    /// <summary>
    ///     Initializes the common captured dimensions.
    /// </summary>
    protected CaptureBuffer(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    /// <summary>Gets the valid BGRA8 pixel memory.</summary>
    public abstract Memory<byte> Memory
    {
        get;
    }

    /// <summary>Gets the captured width in physical pixels.</summary>
    public int Width
    {
        get;
    }

    /// <summary>Gets the captured height in physical pixels.</summary>
    public int Height
    {
        get;
    }

    /// <summary>
    ///     Allocates a buffer through the selected library without boxing its owner.
    /// </summary>
    public static CaptureBuffer Allocate(
        int width,
        int height,
        PixelBufferBackend backend
    ) =>
        backend == PixelBufferBackend.DotNext
            ? new DotNextCaptureBuffer(width, height)
            : new CommunityToolkitCaptureBuffer(width, height);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases owned resources.
    /// </summary>
    /// <param name="disposing">Whether managed resources should be released.</param>
    protected abstract void Dispose(bool disposing);

    private sealed class DotNextCaptureBuffer : CaptureBuffer
    {
        private DotNextBuffer _owner;

        internal DotNextCaptureBuffer(int width, int height)
            : base(width, height)
        {
            _owner = new DotNextBuffer(
                ArrayPool<byte>.Shared,
                checked(width * height * 4)
            );
        }

        public override Memory<byte> Memory => _owner.Memory;

        protected override void Dispose(bool disposing) => _owner.Dispose();
    }

    private sealed class CommunityToolkitCaptureBuffer : CaptureBuffer
    {
        private readonly MemoryOwner<byte> _owner;

        internal CommunityToolkitCaptureBuffer(int width, int height)
            : base(width, height)
        {
            _owner = MemoryOwner<byte>.Allocate(checked(width * height * 4));
        }

        public override Memory<byte> Memory => _owner.Memory;

        protected override void Dispose(bool disposing) => _owner.Dispose();
    }
}