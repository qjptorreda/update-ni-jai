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
            await Task.Delay(2000);

            bool isLoggedIn = Preferences.Default.Get("IsLoggedIn", false);
            bool hasSignedUp = Preferences.Default.Get("HasSignedUp", false);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Application.Current != null)
                {
                    if (isLoggedIn)
                    {
                        bool hasPermissions = Preferences.Default.Get("HasCompletedPermissions", false);
                        if (hasPermissions)
                        {
                            Application.Current.MainPage = new AppShell();
                        }
                        else
                        {
                            var permissionsPage = _serviceProvider.GetRequiredService<PermissionsPage>();
                            Application.Current.MainPage = permissionsPage;
                        }
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



