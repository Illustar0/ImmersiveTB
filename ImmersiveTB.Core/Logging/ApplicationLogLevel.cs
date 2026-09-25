namespace ImmersiveTB.Core.Logging;

/// <summary>
///     Identifies the minimum application log severity.
/// </summary>
public enum ApplicationLogLevel
{
    /// <summary>Records all diagnostic events.</summary>
    Trace,

    /// <summary>Records debugging and higher-severity events.</summary>
    Debug,

    /// <summary>Records informational and higher-severity events.</summary>
    Information,

    /// <summary>Records warnings and higher-severity events.</summary>
    Warning,

    /// <summary>Records errors and critical events.</summary>
    Error,

    /// <summary>Records only critical events.</summary>
    Critical,

    /// <summary>Disables log output.</summary>
    Off
}