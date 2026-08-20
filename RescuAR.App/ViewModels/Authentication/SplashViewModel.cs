using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using RescuAR.App.Views.Authentication;

namespace RescuAR.App.ViewModels.Authentication
{
    public partial class SplashViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _statusText = "Loading safety resources...";

        [ObservableProperty]
        private string _versionText = "v0.0.1a";

        public SplashViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            // Simulate loading safety resources
            await Task.Delay(2500);

            bool isLoggedIn = Preferences.Default.Get("IsLoggedIn", false);
            bool hasSignedUp = Preferences.Default.Get("HasSignedUp", false);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current != null)
                {
                    if (isLoggedIn)
                    {
                        Application.Current.MainPage = new AppShell();
                    }
                    else if (hasSignedUp)
                    {
                        var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
                        Application.Current.MainPage = new NavigationPage(loginPage);
                    }
                    else
                    {
                        var onboardingPage = _serviceProvider.GetRequiredService<OnboardingPage>();
                        Application.Current.MainPage = new NavigationPage(onboardingPage);
                    }
                }
            });
        }
    }
}

