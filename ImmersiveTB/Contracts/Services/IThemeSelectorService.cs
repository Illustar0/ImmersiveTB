using ImmersiveTB.Models;

namespace ImmersiveTB.Contracts.Services;

public interface IThemeSelectorService
{
    AppTheme Theme
    {
        get;
    }

    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    Task InitializeAsync();

    void SetTheme(AppTheme theme);
}

/// <summary>
///     Describes an application theme change.
/// </summary>
public sealed class ThemeChangedEventArgs(AppTheme theme) : EventArgs
{
    /// <summary>Gets the newly selected theme.</summary>
    public AppTheme Theme
    {
        get;
    } = theme;
}