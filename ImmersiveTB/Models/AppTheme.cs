using Microsoft.UI.Xaml;

namespace ImmersiveTB.Models;

public enum AppTheme
{
    Light,
    Dark,
    System
}

public static class AppThemeExtensions
{
    public static ElementTheme ToElementTheme(this AppTheme theme) =>
        theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

    public static AppTheme FromElementTheme(ElementTheme theme) =>
        theme switch
        {
            ElementTheme.Light => AppTheme.Light,
            ElementTheme.Dark => AppTheme.Dark,
            _ => AppTheme.System
        };
}