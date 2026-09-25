using System.Globalization;
using BetterWinUI.PageActivation.DependencyInjection;
using BetterWinUI.Navigation;
using ImmersiveTB.Helpers;
using ImmersiveTB.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;

#pragma warning disable S2325 // Compiled XAML binding requires an instance header.

namespace ImmersiveTB.Views;

/// <summary>
///     Displays application preferences.
/// </summary>
[View]
[PageFor<SettingsViewModel>]
public sealed partial class SettingsPage : Page
{
    /// <summary>
    ///     Initializes the page with its view model.
    /// </summary>
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ViewModel.LanguageRestartRequired += OnLanguageRestartRequired;
    }

    /// <summary>
    ///     Gets the page view model.
    /// </summary>
    public SettingsViewModel ViewModel
    {
        get;
    }

    /// <summary>
    ///     Gets the localized page heading used by compiled XAML binding.
    /// </summary>
    public string Header => "Shell_Settings".GetLocalized();

    /// <summary>
    ///     Informs the user that the selected application language applies after restart.
    /// </summary>
    private void OnLanguageRestartRequired(object? sender, EventArgs args) =>
        _ = ShowLanguageRestartDialogAsync();

    private async Task ShowLanguageRestartDialogAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "LanguageRestartDialog_Title".GetLocalized(),
            Content = "LanguageRestartDialog_Content".GetLocalized(),
            PrimaryButtonText = "LanguageRestartDialog_Confirm".GetLocalized(),
            CloseButtonText = "LanguageRestartDialog_Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var failureReason = AppInstance.Restart(string.Empty);
        var failureDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "LanguageRestartFailedDialog_Title".GetLocalized(),
            Content = string.Format(
                CultureInfo.CurrentCulture,
                "LanguageRestartFailedDialog_Content".GetLocalized(),
                failureReason
            ),
            CloseButtonText = "LanguageRestartFailedDialog_Close".GetLocalized(),
            DefaultButton = ContentDialogButton.Close
        };
        await failureDialog.ShowAsync();
    }
}