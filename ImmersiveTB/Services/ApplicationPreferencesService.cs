using ImmersiveTB.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Logging;
using ImmersiveTB.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

namespace ImmersiveTB.Services;

/// <summary>
/// Edits settings on the UI thread, persists validated copies, and reloads the Options pipeline.
/// </summary>
public sealed class ApplicationPreferencesService : IDisposable
{
    private readonly IOptionsMonitor<ApplicationPreferences> _options;
    private readonly IValidateOptions<ApplicationPreferences> _validator;
    private readonly IApplicationSettings _settings;
    private readonly IConfigurationRoot _configuration;
    private readonly ILogger<ApplicationPreferencesService> _logger;
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly IDisposable? _subscription;
    private ApplicationPreferences? _pending;

    /// <summary>
    /// Connects the settings editor to generated validation, persistence and Options notifications.
    /// </summary>
    public ApplicationPreferencesService(
        IOptionsMonitor<ApplicationPreferences> options,
        IValidateOptions<ApplicationPreferences> validator,
        IApplicationSettings settings,
        IConfigurationRoot configuration,
        IThemeSelectorService themeSelector,
        ILogger<ApplicationPreferencesService> logger)
    {
        _options = options;
        _validator = validator;
        _settings = settings;
        _configuration = configuration;
        _logger = logger;
        _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(300);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += OnSaveTimerTick;
        _subscription = options.OnChange(preferences =>
        {
            themeSelector.SetTheme(preferences.Application.AppTheme);
            LoggingConfiguration.SetLevel(preferences.Logging.MinimumLevel);
        });
        AppLogMessages.SettingsLoaded(logger);
    }

    /// <summary>Gets the current settings, including validated edits awaiting persistence.</summary>
    public ApplicationPreferences Current => _pending ?? _options.CurrentValue;

    /// <summary>Validates a record copy and coalesces consecutive settings edits.</summary>
    public void Update(Func<ApplicationPreferences, ApplicationPreferences> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var next = update(Current);
        var result = _validator.Validate(Options.DefaultName, next);
        if (result.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(ApplicationPreferences), result.Failures);
        }

        if (next == Current)
        {
            return;
        }

        _pending = next;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// Atomically saves pending settings, then rebinds and validates all monitored options.
    /// Also creates the optional user file when the user requests to edit it.
    /// </summary>
    public void Flush()
    {
        _saveTimer.Stop();
        if (_pending is null && File.Exists(_settings.PreferencesFilePath))
        {
            return;
        }

        _settings.SavePreferences(Current);
        _configuration.Reload();
        _pending = null;
        AppLogMessages.SettingPersisted(_logger, Path.GetFileName(_settings.PreferencesFilePath));
    }

    /// <summary>Releases the UI timer and Options change subscription.</summary>
    public void Dispose()
    {
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _subscription?.Dispose();
    }

    /// <summary>Keeps failed writes pending for an explicit flush or subsequent edit.</summary>
    private void OnSaveTimerTick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            Flush();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLogMessages.SettingsSaveFailed(_logger, exception);
        }
    }

    /// <summary>Applies the language override before WinUI resources are initialized.</summary>
    public static void ApplyLanguageOverride(ApplicationLanguage language) =>
        ApplicationLanguages.PrimaryLanguageOverride = language switch
        {
            ApplicationLanguage.English => "en-US",
            ApplicationLanguage.SimplifiedChinese => "zh-Hans",
            _ => GetSystemLanguageTag()
        };

    /// <summary>Chooses the first supported Windows language, falling back to English.</summary>
    private static string GetSystemLanguageTag()
    {
        foreach (var language in GlobalizationPreferences.Languages)
        {
            if (IsSimplifiedChinese(language))
            {
                return "zh-Hans";
            }

            if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return "en-US";
            }
        }

        return "en-US";
    }

    /// <summary>Recognizes Windows language tags for Simplified Chinese.</summary>
    private static bool IsSimplifiedChinese(string language) =>
        language.Equals("zh", StringComparison.OrdinalIgnoreCase)
        || language.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)
        || language.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase)
        || language.StartsWith("zh-SG", StringComparison.OrdinalIgnoreCase);
}