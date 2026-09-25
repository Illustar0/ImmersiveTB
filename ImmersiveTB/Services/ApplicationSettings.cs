using ImmersiveTB.Contracts.Services;
using ImmersiveTB.Helpers;
using ImmersiveTB.Models;
using CsToml;
using Microsoft.Windows.Storage;

namespace ImmersiveTB.Services;

/// <summary>
///     Stores user preferences in a local TOML file.
/// </summary>
public sealed class ApplicationSettings : IApplicationSettings
{
    private const string PublisherName = "Illustar0";
    private const string ProductName = "ImmersiveTB";
    private const string PreferencesFileName = "preferences.toml";

    private static readonly CsTomlSerializerOptions SerializerOptions =
        CsTomlSerializerOptions.Default with
        {
            SerializeOptions = new SerializeOptions
            {
                TableStyle = TomlTableStyle.Header
            }
        };

    /// <summary>
    ///     Initializes the application data directory and preference file path.
    /// </summary>
    public ApplicationSettings()
    {
        LocalFolderPath = RuntimeHelper.IsMSIX
            ? ApplicationData.GetDefault().LocalFolder.Path
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                PublisherName,
                ProductName
            );
        Directory.CreateDirectory(LocalFolderPath);
        PreferencesFilePath = Path.Combine(LocalFolderPath, PreferencesFileName);
    }

    /// <inheritdoc />
    public string LocalFolderPath
    {
        get;
    }

    /// <inheritdoc />
    public string PreferencesFilePath
    {
        get;
    }

    /// <inheritdoc />
    public void SavePreferences(ApplicationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var temporaryPath = Path.Combine(
            LocalFolderPath,
            $".{PreferencesFileName}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp"
        );
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                CsTomlSerializer.Serialize(stream, preferences, SerializerOptions);
                stream.Flush(true);
            }

            File.Move(temporaryPath, PreferencesFilePath, true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}