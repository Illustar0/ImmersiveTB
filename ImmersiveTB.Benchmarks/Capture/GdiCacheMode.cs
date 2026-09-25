namespace ImmersiveTB.Benchmarks.Capture;

/// <summary>
///     Selects GDI resources retained between benchmark invocations.
/// </summary>
[Flags]
internal enum GdiCacheMode
{
    /// <summary>Recreates device contexts and the bitmap for every capture.</summary>
    None = 0,

    /// <summary>Reuses the screen and compatible-memory device contexts.</summary>
    DeviceContexts = 1,

    /// <summary>Reuses the compatible bitmap while its dimensions match.</summary>
    Bitmap = 2,

    /// <summary>Reuses both practical GDI resource groups.</summary>
    All = DeviceContexts | Bitmap
}