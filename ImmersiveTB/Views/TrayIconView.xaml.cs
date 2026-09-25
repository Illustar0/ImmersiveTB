using ImmersiveTB.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;

namespace ImmersiveTB.Views;

/// <summary>
///     Owns the tray icon visual tree and compiled bindings.
/// </summary>
public sealed partial class TrayIconView : UserControl, IDisposable
{
    private bool _disposed;

    /// <summary>
    ///     Initializes the tray icon view.
    /// </summary>
    /// <param name="viewModel">The tray-menu presentation state.</param>
    public TrayIconView(TrayIconViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>Gets the tray-menu presentation state.</summary>
    public TrayIconViewModel ViewModel
    {
        get;
    }

    /// <summary>Requests that the application show its main window.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Requests an orderly application shutdown.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>Initializes compiled bindings and displays the notification-area icon.</summary>
    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Bindings.Update();
        TaskbarIconControl.ForceCreate();
    }

    /// <summary>Removes the icon and disconnects the view from its bindings and owner.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Bindings.StopTracking();
        TaskbarIconControl.Dispose();
        OpenRequested = null;
        ExitRequested = null;
    }

    /// <summary>Raises the window activation request from the icon's left-click command.</summary>
    [RelayCommand]
    private void RequestOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the shutdown request from the menu command.</summary>
    [RelayCommand]
    private void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}