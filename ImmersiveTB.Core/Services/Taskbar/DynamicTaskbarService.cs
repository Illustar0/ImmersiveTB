using System.ComponentModel;
using ImmersiveTB.Core.Logging;
using Microsoft.Extensions.Logging;

namespace ImmersiveTB.Core.Services.Taskbar;

/// <summary>
///     Owns the shared runtime controls for dynamic taskbar behavior.
/// </summary>
public sealed class DynamicTaskbarService(ILogger<DynamicTaskbarService> logger)
    : INotifyPropertyChanged
{
    private bool _isDynamicThemeEnabled = true;
    private bool _isEnabled = true;

    /// <summary>
    ///     Gets or sets whether dynamic taskbar appearance updates are enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            CoreLogMessages.PauseStateChanged(logger, !value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    /// <summary>
    ///     Gets or sets whether sampled colors also update the Windows theme.
    /// </summary>
    public bool IsDynamicThemeEnabled
    {
        get => _isDynamicThemeEnabled;
        set
        {
            if (_isDynamicThemeEnabled == value)
            {
                return;
            }

            _isDynamicThemeEnabled = value;
            CoreLogMessages.DynamicThemeChanged(logger, value);
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(IsDynamicThemeEnabled))
            );
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}