using System.Buffers;
using System.Runtime.InteropServices;
using DotNext.Buffers;

namespace ImmersiveTB.Core.Models;

/// <summary>
///     Identifies an available desktop capture implementation.
/// </summary>
public enum ScreenCaptureMethod
{
    /// <summary>
    ///     Captures with the Windows Graphics Capture API.
    /// </summary>
    WindowsGraphicsCapture,

    /// <summary>
    ///     Captures with a GDI device context.
    /// </summary>
    Gdi
}

/// <summary>
///     Describes a desktop region in physical pixels.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CaptureRegion(int X, int Y, int Width, int Height);

/// <summary>
///     Owns a pooled BGRA8 capture buffer.
/// </summary>
public sealed class CapturedFrame : IDisposable
{
    private MemoryOwner<byte> _owner;

    /// <summary>
    ///     Allocates a pooled frame buffer.
    /// </summary>
    public CapturedFrame(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _owner = new MemoryOwner<byte>(ArrayPool<byte>.Shared, checked(width * height * 4));
    }

    /// <summary>
    ///     Gets the frame width in pixels.
    /// </summary>
    public int Width
    {
        get;
    }

    /// <summary>
    ///     Gets the frame height in pixels.
    /// </summary>
    public int Height
    {
        get;
    }

    /// <summary>
    ///     Gets the valid BGRA8 pixel memory.
    /// </summary>
    public Memory<byte> Pixels => _owner.Memory;

    /// <inheritdoc />
    public void Dispose() => _owner.Dispose();
}