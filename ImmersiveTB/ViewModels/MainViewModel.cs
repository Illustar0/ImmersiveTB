using BetterWinUI.PageActivation.DependencyInjection;
using ImmersiveTB.Core.Services.Taskbar;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace ImmersiveTB.ViewModels;

/// <summary>
///     Exposes main-page dependencies to compiled bindings.
/// </summary>
[ViewModel(ServiceLifetime.Transient)]
public sealed partial class MainViewModel(DynamicTaskbarService dynamicTaskbar)
    : ObservableRecipient
{
    /// <summary>
    ///     Gets the shared dynamic taskbar controls.
    /// </summary>
    public DynamicTaskbarService DynamicTaskbar
    {
        get;
    } = dynamicTaskbar;
}