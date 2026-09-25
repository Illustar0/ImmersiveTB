using System.ComponentModel.DataAnnotations;
using CsToml;
using ImmersiveTB.Core.Logging;

namespace ImmersiveTB.Core.Models;

/// <summary>Configures the minimum application log level.</summary>
[TomlSerializedObject]
public sealed partial record LoggingOptions
{
    /// <summary>Gets or sets the minimum emitted log level.</summary>
    [TomlValueOnSerialized]
    [EnumDataType(typeof(ApplicationLogLevel))]
    public ApplicationLogLevel MinimumLevel
    {
        get;
        set;
    } = ApplicationLogLevel.Information;
}