using System.ComponentModel.DataAnnotations;
using CsToml;

namespace ImmersiveTB.Core.Models;

/// <summary>
///     Configures optional color adjustments after sampling.
/// </summary>
[TomlSerializedObject]
public sealed partial record PostProcessOptions
{
    /// <summary>
    ///     Gets or sets whether post-processing is enabled.
    /// </summary>
    [TomlValueOnSerialized]
    public bool Enabled
    {
        get;
        set;
    }

    /// <summary>
    ///     Gets or sets the brightness adjustment from -1 through 1.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(-1.0, 1.0)]
    public double BrightnessAdjustment
    {
        get;
        set;
    }

    /// <summary>
    ///     Gets or sets the saturation adjustment from -1 through 1.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(-1.0, 1.0)]
    public double SaturationAdjustment
    {
        get;
        set;
    }

    /// <summary>
    ///     Gets or sets the contrast adjustment from -1 through 1.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(-1.0, 1.0)]
    public double ContrastAdjustment
    {
        get;
        set;
    }

    /// <summary>
    ///     Gets or sets opacity from 0 through 1.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(0.0, 1.0)]
    public double Opacity
    {
        get;
        set;
    } = 1;
}