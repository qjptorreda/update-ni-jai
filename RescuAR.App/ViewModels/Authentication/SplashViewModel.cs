using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using RescuAR.App.Views.Authentication;

namespace RescuAR.App.ViewModels.Authentication
{
    public partial class SplashViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;
        private bool _hasNavigated = false;

        [ObservableProperty]
        private string _statusText = "Calibrating AR Evacuation System...";

        [ObservableProperty]
        private string _versionText = "v1.0.0 • RescuAR";

        public SplashViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            StatusText = "Scanning Safe Routes...";
            await Task.Delay(1000);

            if (_hasNavigated) return;
            StatusText = "AR Evacuation Guidance Active...";
            await Task.Delay(1000);

            if (_hasNavigated) return;
            StatusText = "Ready • Entering RescuAR...";
            await Task.Delay(1000);

            NavigateToNextPage();
        }

        public void SkipToNextPage()
        {
            NavigateToNextPage();
        }

        private void NavigateToNextPage()
        {
            if (_hasNavigated) return;
            _hasNavigated = true;

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


