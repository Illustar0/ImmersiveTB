using System.ComponentModel.DataAnnotations;
using ImmersiveTB.Core.Services;
using CsToml;
using Microsoft.Extensions.Options;

namespace ImmersiveTB.Core.Models;

/// <summary>
///     Configures desktop capture and color reduction.
/// </summary>
[TomlSerializedObject]
public sealed partial record ColorSamplingOptions
{
    /// <summary>
    ///     Gets or sets the desktop capture implementation.
    /// </summary>
    [TomlValueOnSerialized]
    [EnumDataType(typeof(ScreenCaptureMethod))]
    public ScreenCaptureMethod CaptureMethod
    {
        get;
        set;
    } =
        ScreenCaptureMethod.WindowsGraphicsCapture;

    /// <summary>
    ///     Gets or sets the sampled region height in physical pixels.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(1, 100)]
    public int SampleHeight
    {
        get;
        set;
    } = 10;

    /// <summary>
    ///     Gets or sets the gap above the taskbar in physical pixels.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(0, 50)]
    public int TaskbarOffset
    {
        get;
        set;
    } = 1;

    /// <summary>
    ///     Gets or sets the color reduction algorithm.
    /// </summary>
    [TomlValueOnSerialized]
    [EnumDataType(typeof(ColorSamplingAlgorithm))]
    public ColorSamplingAlgorithm Algorithm
    {
        get;
        set;
    } = ColorSamplingAlgorithm.DominantColor;

    /// <summary>
    ///     Gets or sets Gaussian sampling configuration.
    /// </summary>
    [TomlValueOnSerialized]
    [Required]
    [ValidateObjectMembers]
    public GaussianBlurOptions GaussianBlurOptions
    {
        get;
        set;
    } = new();
}

/// <summary>
///     Configures Gaussian center sampling.
/// </summary>
[TomlSerializedObject]
public sealed partial record GaussianBlurOptions
{
    /// <summary>
    ///     Gets or sets the kernel radius in pixels.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(1, 50)]
    public int Radius
    {
        get;
        set;
    } = 20;
}