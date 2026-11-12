using Microsoft.Extensions.DependencyInjection;

namespace HQ_Character_Tracker;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UserAppTheme = Preferences.Get("AppTheme", "Dark") switch
        {
            "Light" => AppTheme.Light,
            "System" => AppTheme.Unspecified,
            _ => AppTheme.Dark
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}