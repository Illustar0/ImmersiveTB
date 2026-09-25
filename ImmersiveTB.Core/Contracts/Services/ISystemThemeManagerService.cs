using System.Drawing;
using ImmersiveTB.Core.Models;

namespace ImmersiveTB.Core.Contracts.Services;

/// <summary>
///     Reads, updates, and broadcasts the Windows system theme.
/// </summary>
public interface ISystemThemeManagerService
{
    /// <summary>
    ///     Sets the system theme when its persisted value differs.
    /// </summary>
    /// <returns><see langword="true" /> when the persisted theme changed.</returns>
    bool SetSystemTheme(SystemTheme theme);

    /// <summary>
    ///     Restores the Windows theme value that was present before the latest application override.
    ///     Leaves a value changed elsewhere untouched.
    /// </summary>
    /// <returns><see langword="true" /> when the persisted theme changed.</returns>
    bool RestoreSystemTheme();

    /// <summary>
    ///     Gets the persisted Windows system theme.
    /// </summary>
    SystemTheme GetSystemTheme();

    /// <summary>
    ///     Selects a readable system theme for the supplied background color.
    /// </summary>
    SystemTheme GetAdaptedTheme(Color color);

    /// <summary>
    ///     Broadcasts the immersive color-setting change to a window.
    /// </summary>
    Task BroadcastSettingChange(IntPtr hwnd);
}