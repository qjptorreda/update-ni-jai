using RescuAR.App.Views.Authentication;

namespace RescuAR.App;

public partial class App : Application
{
    public App(SplashPage splashPage, LoginPage loginPage)
    {
        InitializeComponent();
        MainPage = splashPage;
    }


    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(MainPage ?? new AppShell());
    }
}
