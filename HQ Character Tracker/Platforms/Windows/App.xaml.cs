using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using HQ_Character_Tracker.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HQ_Character_Tracker.WinUI;
/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var appInstance = AppInstance.GetCurrent();
        appInstance.Activated += (_, activationArgs) => QueueFileActivation(activationArgs);
        QueueFileActivation(appInstance.GetActivatedEventArgs());
        base.OnLaunched(args);
    }

    private static void QueueFileActivation(AppActivationArguments activationArgs)
    {
        if (activationArgs.Kind != ExtendedActivationKind.File ||
            activationArgs.Data is not Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs)
            return;

        var path = fileArgs.Files.FirstOrDefault()?.Path;
        if (!string.IsNullOrWhiteSpace(path))
            WindowsFileActivation.Queue(path);
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

