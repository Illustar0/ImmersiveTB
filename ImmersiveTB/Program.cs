using Microsoft.Windows.AppLifecycle;

namespace ImmersiveTB;

/// <summary>Redirects later launches to the running application instance.</summary>
internal static class Program
{
    private const string InstanceKey = "ImmersiveTB";

    private static readonly TaskCompletionSource<App> AppReady = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    /// <summary>Registers the first instance before starting XAML.</summary>
    [STAThread]
    private static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (!instance.IsCurrent)
        {
            instance.RedirectActivationToAsync(activation).GetAwaiter().GetResult();
            return;
        }

        instance.Activated += OnActivated;
        XamlGeneratedProgram.XamlGeneratedMain();
    }

    /// <summary>Releases redirected launches once the tray application is ready.</summary>
    internal static void NotifyLaunched(App app) => AppReady.TrySetResult(app);

    private static async void OnActivated(object? sender, AppActivationArguments args)
    {
        var app = await AppReady.Task.ConfigureAwait(false);
        app.OpenFromRedirectedActivation();
    }
}