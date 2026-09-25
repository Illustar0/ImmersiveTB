using ImmersiveTB.Contracts.Services;
using ImmersiveTB.Logging;
using ImmersiveTB.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImmersiveTB.Services;

/// <summary>
///     Applies the application's requested theme to active windows.
/// </summary>
public sealed class ThemeSelectorService(
    IOptionsMonitor<ApplicationPreferences> preferences,
    ILogger<ThemeSelectorService> logger
) : IThemeSelectorService
{
    private AppTheme _theme = preferences.CurrentValue.Application.AppTheme;

    /// <inheritdoc />
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <inheritdoc />
    public AppTheme Theme
    {
        get => _theme;
        private set
        {
            if (_theme == value)
            {
                return;
            }

            _theme = value;
            AppLogMessages.ThemeChanged(logger, value);
            ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(value));
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        AppLogMessages.ThemeInitialized(logger, Theme);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void SetTheme(AppTheme theme) => Theme = theme;
}