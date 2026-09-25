using BetterWinUI.PageActivation;
using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.Navigation;
using BetterWinUI.Navigation.Frame;
using CsToml.Extensions.Configuration;
using ImmersiveTB.Adapters.ScreenCapture;
using ImmersiveTB.Contracts.Services;
using ImmersiveTB.Core.Contracts.Services;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Services;
using ImmersiveTB.Core.Services.Taskbar;
using ImmersiveTB.Helpers;
using ImmersiveTB.Logging;
using ImmersiveTB.Models;
using ImmersiveTB.Services;
using ImmersiveTB.ViewModels;
using ImmersiveTB.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml;
using Serilog;
using UnhandledExceptionEventArgs = Microsoft.UI.Xaml.UnhandledExceptionEventArgs;

#pragma warning disable MA0004 // WinUI lifecycle code must resume on the UI context.
#pragma warning disable MA0147 // WinUI lifecycle overrides and event handlers require async void.
#pragma warning disable MA0051 // Application composition stays together at the entry point.

namespace ImmersiveTB;

/// <summary>Composes the application and coordinates its top-level views and host lifetime.</summary>
[GeneratePageActivationHook]
public sealed partial class App : Application, IDisposable
{
    private readonly IDispatcherService _dispatcher;
    private readonly ILogger<App> _logger;
    private readonly AppNotificationService? _notifications;
    private readonly ApplicationPreferencesService _preferencesService;
    private MainWindow? _mainWindow;
    private TrayIconView? _trayIconView;
    private bool _isExiting;

    /// <summary>Builds the host and initializes application resources and notifications.</summary>
    public App()
    {
        var applicationSettings = new ApplicationSettings();
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
                DisableDefaults = true
            }
        );
        builder.Configuration.Sources.Clear();
        builder.Configuration.SetBasePath(AppContext.BaseDirectory)
            .AddTomlFile("appsettings.toml", false, false)
            .AddTomlFile(applicationSettings.PreferencesFilePath, true, false);
        var services = builder.Services;
        services.AddSingleton<IApplicationSettings>(applicationSettings);
        services.AddSingleton<IConfigurationRoot>(builder.Configuration);
        services.AddApplicationOptions(builder.Configuration);
        services.AddSerilog((provider, logging) => LoggingConfiguration.Configure(
            logging,
            Path.Combine(applicationSettings.LocalFolderPath, "Logs"),
            provider.GetRequiredService<IOptionsMonitor<ApplicationPreferences>>().CurrentValue.Logging.MinimumLevel
        ));

        if (RuntimeHelper.IsMSIX)
        {
            services.AddSingleton<AppNotificationService>();
        }

        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddSingleton<ApplicationPreferencesService>();
        services.AddSingleton<IDispatcherService, DispatcherService>();
        services.AddSingleton<IScreenCapture, GdiScreenCapture>();
        services.AddSingleton<IScreenCapture, GraphicsCaptureScreenCapture>();
        services.AddSingleton<ITaskbarStateService, TaskbarStateService>();
        services.AddHostedService<TaskbarStateService>(sp =>
            (TaskbarStateService)sp.GetRequiredService<ITaskbarStateService>());
        services.AddSingleton<IColorSamplerService, ColorSamplerService>();
        services.AddSingleton<ITaskbarManagerService, TranslucentTbManagerService>();
        services.AddHostedService<TranslucentTbManagerService>(sp =>
            (TranslucentTbManagerService)sp.GetRequiredService<ITaskbarManagerService>());
        services.AddSingleton<ISystemThemeManagerService, SystemThemeManagerService>();
        services.AddSingleton<DynamicTaskbarService>();
        services.AddSingleton<TaskbarAppearanceUpdater>();
        services.AddSingleton<TaskbarAppearanceOrchestrator>();
        services.AddHostedService<TaskbarAppearanceOrchestrator>(sp =>
            sp.GetRequiredService<TaskbarAppearanceOrchestrator>());
        var pageMap = new PageMap();
        pageMap.AddGeneratedPages();
        services.AddSingleton(new FrameNavigator(pageMap));
        services.AddSingleton<TrayIconViewModel>();
        services.AddGeneratedPageServices();
        Host = builder.Build();

        var preferences = Host.Services.GetRequiredService<IOptionsMonitor<ApplicationPreferences>>().CurrentValue;
        ApplicationPreferencesService.ApplyLanguageOverride(preferences.Application.ApplicationLanguage);
        UsePageActivation(Host.Services);
        InitializeComponent();


        _logger = Host.Services.GetRequiredService<ILogger<App>>();
        AppLogMessages.ApplicationStarting(_logger);

        _preferencesService = Host.Services.GetRequiredService<ApplicationPreferencesService>();
        _dispatcher = Host.Services.GetRequiredService<IDispatcherService>();
        if (RuntimeHelper.IsMSIX)
        {
            _notifications = Host.Services.GetRequiredService<AppNotificationService>();
            _notifications.Invoked += OnNotificationInvoked;
            _notifications.Initialize();
        }

        UnhandledException += App_UnhandledException;
    }

    /// <summary>Gets the host owned by this application instance.</summary>
    private IHost Host
    {
        get;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var trayIconView = _trayIconView;
        if (trayIconView is null)
        {
            return;
        }

        _trayIconView = null;
        trayIconView.OpenRequested -= OnOpenRequested;
        trayIconView.ExitRequested -= OnExitRequested;
        trayIconView.Dispose();
    }

    /// <summary>Logs unhandled WinUI exceptions.</summary>
    private void App_UnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        AppLogMessages.UnhandledException(_logger, args.Exception);
        Log.CloseAndFlush();
    }

    /// <summary>Starts background services before exposing the tray entry point.</summary>
    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        await Host.StartAsync();
        _trayIconView = new TrayIconView(Host.Services.GetRequiredService<TrayIconViewModel>());
        _trayIconView.OpenRequested += OnOpenRequested;
        _trayIconView.ExitRequested += OnExitRequested;
        _trayIconView.Show();
        Program.NotifyLaunched(this);
        AppLogMessages.TrayIconInitialized(_logger);
        AppLogMessages.ApplicationStarted(_logger);
    }

    /// <summary>Opens the main window when another launch is redirected here.</summary>
    internal void OpenFromRedirectedActivation() => OnOpenRequested(this, EventArgs.Empty);

    /// <summary>Routes notification activation through the same window entry point as the tray.</summary>
    private void OnNotificationInvoked(object? sender, EventArgs args)
    {
        AppLogMessages.NotificationInvoked(_logger);
        OnOpenRequested(sender, args);
    }

    /// <summary>Serializes window requests on the UI dispatcher.</summary>
    private void OnOpenRequested(object? sender, EventArgs args)
    {
        if (!_dispatcher.TryEnqueue(ShowMainWindow))
        {
            AppLogMessages.MainWindowDispatchFailed(_logger);
        }
    }

    /// <summary>Lazily creates the main window and delegates window behavior to its code-behind.</summary>
    private void ShowMainWindow()
    {
        if (_isExiting)
        {
            return;
        }

        try
        {
            _mainWindow ??= ActivatorUtilities.CreateInstance<MainWindow>(Host.Services);
            _mainWindow.ShowAndActivate();
        }
        catch (Exception exception)
        {
            AppLogMessages.MainWindowActivationFailed(_logger, exception);
        }
    }

    /// <summary>Saves settings and shuts down once, while the UI dispatcher is still available.</summary>
    private async void OnExitRequested(object? sender, EventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        try
        {
            _preferencesService.Flush();
        }
        catch (Exception exception)
        {
            _isExiting = false;
            AppLogMessages.SettingsSaveFailed(_logger, exception);
            return;
        }

        _mainWindow?.PrepareForExit();
        if (_notifications is not null)
        {
            _notifications.Invoked -= OnNotificationInvoked;
        }

        Dispose();
        AppLogMessages.ApplicationStopping(_logger);
        try
        {
            await Host.StopAsync();
        }
        catch (Exception exception)
        {
            AppLogMessages.UnhandledException(_logger, exception);
        }
        finally
        {
            Host.Dispose();
            await Log.CloseAndFlushAsync();
            Current.Exit();
        }
    }
}