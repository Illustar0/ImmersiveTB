using System.ComponentModel.DataAnnotations;
using CsToml;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Options;

namespace ImmersiveTB.Models;

/// <summary>Identifies the application language selected by the user.</summary>
public enum ApplicationLanguage
{
    /// <summary>Uses the Windows language preference.</summary>
    System,

    /// <summary>Uses English resources.</summary>
    English,

    /// <summary>Uses Simplified Chinese resources.</summary>
    SimplifiedChinese
}

/// <summary>
/// Defines the shared schema for application defaults, user overrides and settings edits.
/// Published options instances are read-only by convention; edits use record copies.
/// </summary>
[TomlSerializedObject]
public sealed partial record ApplicationPreferences
{
    /// <summary>Gets or sets application appearance and language.</summary>
    [TomlValueOnSerialized]
    [Required]
    [ValidateObjectMembers]
    public ApplicationPreferenceOptions Application
    {
        get;
        set;
    } = new();

    /// <summary>Gets or sets desktop color sampling options.</summary>
    [TomlValueOnSerialized]
    [Required]
    [ValidateObjectMembers]
    public ColorSamplingOptions ColorSampling
    {
        get;
        set;
    } = new();

    /// <summary>Gets or sets taskbar animation options.</summary>
    [TomlValueOnSerialized]
    [Required]
    [ValidateObjectMembers]
    public TaskbarAppearanceOptions TaskbarAppearance
    {
        get;
        set;
    } = new();

    /// <summary>Gets or sets sampled-color adjustments.</summary>
    [TomlValueOnSerialized]
    [Required]
    [ValidateObjectMembers]
    public PostProcessOptions PostProcess
    {
        get;
        set;
    } = new();

    /// <summary>Gets or sets application logging options.</summary>
    [TomlValueOnSerialized]
    [Required]
    [ValidateObjectMembers]
    public LoggingOptions Logging
    {
        get;
        set;
    } = new();
}

/// <summary>Configures application appearance and language.</summary>
[TomlSerializedObject]
public sealed partial record ApplicationPreferenceOptions
{
    /// <summary>Gets or sets the requested application theme.</summary>
    [TomlValueOnSerialized]
    [EnumDataType(typeof(AppTheme))]
    public AppTheme AppTheme
    {
        get;
        set;
    } = AppTheme.System;

    /// <summary>Gets or sets the language applied on the next launch.</summary>
    [TomlValueOnSerialized]
    [EnumDataType(typeof(ApplicationLanguage))]
    public ApplicationLanguage ApplicationLanguage
    {
        get;
        set;
    }
}