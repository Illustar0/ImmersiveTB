using System.ComponentModel.DataAnnotations;
using ImmersiveTB.Core.Helpers;
using CsToml;

namespace ImmersiveTB.Core.Models;

/// <summary>
///     Configures how sampled colors are applied to the taskbar.
/// </summary>
[TomlSerializedObject]
public sealed partial record TaskbarAppearanceOptions
{
    /// <summary>
    ///     Gets or sets whether taskbar color changes are animated.
    /// </summary>
    [TomlValueOnSerialized]
    public bool ColorAnimationEnabled
    {
        get;
        set;
    } = true;

    /// <summary>
    ///     Gets or sets the easing curve used by taskbar color animations.
    /// </summary>
    [TomlValueOnSerialized]
    [EnumDataType(typeof(EasingType))]
    public EasingType ColorEasing
    {
        get;
        set;
    } = EasingType.EaseOutCubic;

    /// <summary>
    ///     Gets or sets the target color animation frame rate, in frames per second.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(1, 240)]
    public int ColorAnimationFrameRate
    {
        get;
        set;
    } = 60;

    /// <summary>
    ///     Gets or sets the color animation duration in milliseconds.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(0, 5000)]
    public int ColorAnimationDurationMs
    {
        get;
        set;
    } = 750;

    /// <summary>
    ///     Gets or sets the delay before sampling a newly maximized window, in milliseconds.
    /// </summary>
    [TomlValueOnSerialized]
    [Range(0, 2000)]
    public int ColorSamplingDelayMs
    {
        get;
        set;
    } = 250;
}