using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.Navigation.Frame;
using ImmersiveTB.Helpers;
using ImmersiveTB.Logging;
using ImmersiveTB.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace ImmersiveTB.Views;

/// <summary>
///     Preserves the previous custom title-bar shell implementation.
/// </summary>
[View]
public sealed partial class ShellPage : Page
{
    private readonly ILogger<ShellPage> _logger;
    private readonly FrameNavigator _navigation;
    private IDisposable? _frameAttachment;

    /// <summary>
    ///     Initializes the preserved custom shell.
    /// </summary>
    public ShellPage(
        FrameNavigator navigation,
        ILogger<ShellPage> logger
    )
    {
        _navigation = navigation;
        _logger = logger;

        InitializeComponent();

        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));
    }

    /// <summary>
    ///     Gets the element used as the main window title bar.
    /// </summary>
    public UIElement TitleBarElement => AppTitleBar;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_frameAttachment is not null)
        {
            return;
        }

        UpdateTitleBarLayout(NavigationViewControl, NavigationViewControl.DisplayMode);
        _frameAttachment = _navigation.Attach(NavigationFrame);
        NavigationFrame.Navigated += OnNavigated;
        if (
            NavigationFrame.Content is null
            && NavigationViewControl.MenuItems.OfType<NavigationViewItem>().FirstOrDefault()
                is { Tag: string route }
        )
        {
            NavigateToRoute(route);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        NavigationFrame.Navigated -= OnNavigated;
        _frameAttachment?.Dispose();
        _frameAttachment = null;
    }

    private void NavigationViewControl_ItemInvoked(
        NavigationView sender,
        NavigationViewItemInvokedEventArgs args
    )
    {
        if (args.InvokedItemContainer is NavigationViewItem { Tag: string route })
        {
            NavigateToRoute(route);
        }
    }

    private void NavigationViewControl_DisplayModeChanged(
        NavigationView sender,
        NavigationViewDisplayModeChangedEventArgs args
    ) => UpdateTitleBarLayout(sender, args.DisplayMode);

    private void UpdateTitleBarLayout(
        NavigationView navigationView,
        NavigationViewDisplayMode displayMode
    )
    {
        var titleWidth = AppTitleBarContent.DesiredSize.Width;
        AppLogMessages.TitleBarMeasured(_logger, titleWidth);
        if (displayMode == NavigationViewDisplayMode.Minimal)
        {
            navigationView.PaneTitle = "";
            TitleBarLeftSpacer.Width = new GridLength(navigationView.CompactPaneLength * 2);
            TitleBarContentColumn.Width = new GridLength(1, GridUnitType.Star);
            TitleBarRightSpacer.Width = new GridLength(0);
            AppTitleBarContent.HorizontalAlignment = HorizontalAlignment.Left;
            SetOpenPaneLength(navigationView, titleWidth + 16);
            return;
        }

        navigationView.PaneTitle = ResourceExtensions.GetLocalized("Shell_NavigationView/PaneTitle");
        TitleBarLeftSpacer.Width = new GridLength(navigationView.CompactPaneLength);
        TitleBarContentColumn.Width = new GridLength(1, GridUnitType.Star);
        TitleBarRightSpacer.Width = new GridLength(1, GridUnitType.Star);
        AppTitleBarContent.HorizontalAlignment = HorizontalAlignment.Left;
        SetOpenPaneLength(navigationView, titleWidth);
    }

    private static void SetOpenPaneLength(NavigationView navigationView, double titleWidth)
    {
        var openPaneLength = titleWidth + navigationView.CompactPaneLength * 2;
        if (Math.Abs(navigationView.OpenPaneLength - openPaneLength) > 0.1)
        {
            navigationView.OpenPaneLength = openPaneLength;
        }
    }

    private void NavigationViewControl_BackRequested(
        NavigationView sender,
        NavigationViewBackRequestedEventArgs args
    ) => _navigation.GoBack();

    private KeyboardAccelerator BuildKeyboardAccelerator(
        VirtualKey key,
        VirtualKeyModifiers? modifiers = null
    )
    {
        var accelerator = new KeyboardAccelerator { Key = key };
        if (modifiers is { } value)
        {
            accelerator.Modifiers = value;
        }

        accelerator.Invoked += OnKeyboardAcceleratorInvoked;
        return accelerator;
    }

    private void OnKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args
    )
    {
        if (!_navigation.CanGoBack)
        {
            return;
        }

        _navigation.GoBack();
        args.Handled = true;
    }

    private void OnNavigated(object sender, NavigationEventArgs args)
    {
        AppLogMessages.Navigated(_logger, args.SourcePageType.Name);
        NavigationViewControl.IsBackEnabled = _navigation.CanGoBack;

        string? route = null;
        if (args.SourcePageType == typeof(MainPage))
        {
            route = "home";
        }
        else if (args.SourcePageType == typeof(SettingsPage))
        {
            route = "settings";
        }

        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems
            .Concat(NavigationViewControl.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => Equals(item.Tag, route));
    }

    private void NavigateToRoute(string route)
    {
        switch (route)
        {
            case "home" when NavigationFrame.CurrentSourcePageType != typeof(MainPage):
                _navigation.Navigate<MainViewModel>();
                break;
            case "settings" when NavigationFrame.CurrentSourcePageType != typeof(SettingsPage):
                _navigation.Navigate<SettingsViewModel>();
                break;
        }
    }
}