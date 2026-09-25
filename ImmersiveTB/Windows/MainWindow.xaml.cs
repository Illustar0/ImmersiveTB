using ImmersiveTB.Contracts.Services;
using ImmersiveTB.Helpers;
using ImmersiveTB.Logging;
using ImmersiveTB.Models;
using ImmersiveTB.Views;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WindowExtensions = H.NotifyIcon.WindowExtensions;

namespace ImmersiveTB;

/// <summary>
///     Hosts the application's primary XAML content.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IThemeSelectorService _themeSelector;
    private readonly ILogger<MainWindow> _logger;
    private bool _isExiting;

    /// <summary>
    ///     Initializes the main window and its shell content.
    /// </summary>
    /// <param name="shell">The application navigation shell.</param>
    /// <param name="themeSelector">The current theme and its change notifications.</param>
    /// <param name="logger">Records window lifecycle and theme changes.</param>
    public MainWindow(ShellPage shell, IThemeSelectorService themeSelector, ILogger<MainWindow> logger)
    {
        _themeSelector = themeSelector;
        _logger = logger;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        RootContent.Content = shell;
        SetTitleBar(shell.TitleBarElement);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));
        Title = "AppDisplayName".GetLocalized();

        this.CenterOnScreen(800, 600);
        var windowManager = WindowManager.Get(this);
        windowManager.MinWidth = 560;
        windowManager.MinHeight = 470;
        windowManager.PersistenceId = "MainWindow";
        ApplyTheme(themeSelector.Theme);
        themeSelector.ThemeChanged += OnThemeChanged;
        Closed += OnClosed;
        AppLogMessages.MainWindowCreated(logger);
    }

    /// <summary>Restores the window from the tray and brings it to the foreground.</summary>
    public void ShowAndActivate()
    {
        if (_isExiting)
        {
            return;
        }

        WindowExtensions.Show(this, true);
        Activate();
        AppLogMessages.MainWindowActivated(_logger);
    }

    /// <summary>
    ///     Applies the selected application theme to the window content.
    /// </summary>
    /// <param name="theme">The selected application theme.</param>
    private void ApplyTheme(AppTheme theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme.ToElementTheme();
            AppLogMessages.ApplicationThemeApplied(_logger, theme);
        }
    }

    /// <summary>
    ///     Allows the window to close during application shutdown.
    /// </summary>
    public void PrepareForExit()
    {
        _isExiting = true;
        _themeSelector.ThemeChanged -= OnThemeChanged;
    }

    /// <summary>Applies theme changes on the window's dispatcher.</summary>
    private void OnThemeChanged(object? sender, ThemeChangedEventArgs args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyTheme(args.Theme);
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isExiting)
            {
                ApplyTheme(args.Theme);
            }
        });
    }

    /// <summary>Hides the window until the application explicitly begins shutdown.</summary>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        args.Handled = true;
        WindowExtensions.Hide(this);
    }
}