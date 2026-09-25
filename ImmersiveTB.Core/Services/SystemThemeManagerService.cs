using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

#pragma warning disable S6640 // Win32 interop requires unsafe buffers and pointers.

namespace ImmersiveTB.Core.Services;

/// <summary>
///     Reads, updates, and broadcasts the Windows system theme.
/// </summary>
[SupportedOSPlatform("windows5.0")]
public sealed class SystemThemeManagerService(ILogger<SystemThemeManagerService> logger)
    : ISystemThemeManagerService
{
    private const string PersonalizeKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string SystemUsesLightThemeValueName = "SystemUsesLightTheme";
    private object? _originalValue;
    private RegistryValueKind _originalKind;
    private int? _lastAppliedValue;

    /// <inheritdoc />
    public bool SetSystemTheme(SystemTheme theme)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, true);
        if (key is null)
        {
            CoreLogMessages.ThemeRegistryUnavailable(logger);
            return false;
        }

        var currentValue = key.GetValue(SystemUsesLightThemeValueName);
        if (_lastAppliedValue is int lastAppliedValue
            && (currentValue is not int currentThemeValue || currentThemeValue != lastAppliedValue))
        {
            _lastAppliedValue = null;
        }

        var value = theme == SystemTheme.Light ? 1 : 0;
        if (currentValue is int current && current == value)
        {
            return false;
        }

        if (_lastAppliedValue is null)
        {
            _originalValue = currentValue;
            if (currentValue is not null)
            {
                _originalKind = key.GetValueKind(SystemUsesLightThemeValueName);
            }
        }

        key.SetValue(
            SystemUsesLightThemeValueName,
            value,
            RegistryValueKind.DWord
        );
        _lastAppliedValue = value;
        CoreLogMessages.SystemThemeChanged(logger, theme);
        return true;
    }

    /// <inheritdoc />
    public bool RestoreSystemTheme()
    {
        if (_lastAppliedValue is not int lastAppliedValue)
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, true);
        if (key is null)
        {
            CoreLogMessages.ThemeRegistryUnavailable(logger);
            return false;
        }

        if (key.GetValue(SystemUsesLightThemeValueName) is not int currentValue
            || currentValue != lastAppliedValue)
        {
            return false;
        }

        if (_originalValue is null)
        {
            key.DeleteValue(SystemUsesLightThemeValueName, false);
        }
        else
        {
            key.SetValue(SystemUsesLightThemeValueName, _originalValue, _originalKind);
        }

        _lastAppliedValue = null;
        _originalValue = null;
        CoreLogMessages.SystemThemeRestored(logger);
        return true;
    }

    /// <inheritdoc />
    public Task BroadcastSettingChange(IntPtr hwnd) =>
        Task.Run(() =>
        {
            const uint wmSettingChange = 0x001A;

            unsafe
            {
                fixed (char* parameter = "ImmersiveColorSet")
                {
                    var result = PInvoke.SendMessageTimeout(
                        (HWND)hwnd,
                        wmSettingChange,
                        0,
                        (nint)parameter,
                        SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
                        2000,
                        null
                    );

                    if (result == 0)
                    {
                        CoreLogMessages.ThemeBroadcastFailed(logger);
                    }
                }
            }
        });

    /// <inheritdoc />
    public SystemTheme GetSystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        if (key?.GetValue(SystemUsesLightThemeValueName) is int value)
        {
            return value == 0 ? SystemTheme.Dark : SystemTheme.Light;
        }

        CoreLogMessages.ThemeRegistryUnavailable(logger);
        return SystemTheme.Light;
    }

    /// <inheritdoc />
    public SystemTheme GetAdaptedTheme(Color color)
    {
        var backgroundLuminance = GetRelativeLuminance(color);
        var contrastWithWhite = 1.05 / (backgroundLuminance + 0.05);
        var contrastWithBlack = (backgroundLuminance + 0.05) / 0.05;
        return contrastWithBlack > contrastWithWhite ? SystemTheme.Light : SystemTheme.Dark;
    }

    private static double GetRelativeLuminance(Color color)
    {
        var red = Linearize(color.R / 255.0);
        var green = Linearize(color.G / 255.0);
        var blue = Linearize(color.B / 255.0);

        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static double Linearize(double value) =>
        value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
}