using Microsoft.Maui.Controls;
using System;

namespace RescuAR.App.Views.Authentication
{
    public partial class PrivacyPolicyPage : ContentPage
    {
        public PrivacyPolicyPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (Application.Current?.MainPage is NavigationPage navPage)
            {
                await navPage.PopAsync();
            }
        }
    }
}
