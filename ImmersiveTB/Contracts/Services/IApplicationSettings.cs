using ImmersiveTB.Models;

namespace ImmersiveTB.Contracts.Services;

/// <summary>
///     Provides the application's local data path and TOML preferences.
/// </summary>
public interface IApplicationSettings
{
    /// <summary>
    ///     Gets the local application data directory.
    /// </summary>
    string LocalFolderPath
    {
        get;
    }

    /// <summary>
    ///     Gets the user preference file path.
    /// </summary>
    string PreferencesFilePath
    {
        get;
    }

    /// <summary>
    ///     Atomically saves the complete preference snapshot.
    /// </summary>
    void SavePreferences(ApplicationPreferences preferences);
}