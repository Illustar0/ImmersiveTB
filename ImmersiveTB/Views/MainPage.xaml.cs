using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.Navigation;
using ImmersiveTB.Helpers;
using ImmersiveTB.ViewModels;
using Microsoft.UI.Xaml.Controls;

#pragma warning disable S2325 // Compiled XAML binding requires an instance header.

namespace ImmersiveTB.Views;

[View]
[PageFor<MainViewModel>]
public sealed partial class MainPage : Page
{
    /// <summary>
    ///     Initializes the page with its view model.
    /// </summary>
    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>
    ///     Gets the page view model.
    /// </summary>
    public MainViewModel ViewModel
    {
        get;
    }

    /// <summary>
    ///     Gets the localized page heading used by compiled XAML binding.
    /// </summary>
    public string Header => "Shell_MainHeader".GetLocalized();
}