using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using RescuAR.App.Models;
using RescuAR.App.Services.Reports;
using RescuAR.App.Services.Weather;

namespace RescuAR.App.ViewModels.Dashboard;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IWeatherService _weatherService;

    [ObservableProperty]
    public partial string UserName { get; set; } = "User";

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Good day,";

    [ObservableProperty]
    public partial int PreparednessScore { get; set; } = 100;

    [ObservableProperty]
    public partial double ScoreProgress { get; set; } = 1.0;

    [ObservableProperty]
    public partial string PreparednessStatus { get; set; } = "Highly Prepared";

    [ObservableProperty]
    public partial int ActiveAdvisoriesCount { get; set; } = 2;

    [ObservableProperty]
    public partial string WeatherSummary { get; set; } = "24 °C • Clear / Sunny";

    [ObservableProperty]
    public partial string WeatherTemperatureText { get; set; } = "24 °C";

    [ObservableProperty]
    public partial string WeatherConditionTitle { get; set; } = "Clear / Sunny";

    [ObservableProperty]
    public partial string WeatherConditionSummary { get; set; } = "Clear weather conditions in your area";

    [ObservableProperty]
    public partial string LocationName { get; set; } = "Quezon City, Metro Manila";

    [ObservableProperty]
    public partial string FloodRiskLevel { get; set; } = "Moderate Flood Risk";

    [ObservableProperty]
    public partial string RiverStatusText { get; set; } = "Marikina River: Level 1 (Standby)";

    [ObservableProperty]
    public partial string RiverStatusPillText { get; set; } = "Monitoring";

    [ObservableProperty]
    public partial DisasterAdvisory? SelectedAdvisory { get; set; }

    [ObservableProperty]
    public partial bool IsPopupVisible { get; set; }

    [ObservableProperty]
    public partial bool IsPermissionsPopupVisible { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<DashboardCarouselItem> CarouselItems { get; set; } = new();

    public DashboardViewModel()
    {
        _weatherService = WeatherService.Instance;

        CarouselItems = new ObservableCollection<DashboardCarouselItem>
        {
            new DashboardCarouselItem
            {
                Id = "1",
                Title = "Marikina Flood History",
                Description = "Learn from past floods through photos, documentaries, and news archives to improve your disaster preparedness.",
                ButtonText = "Learn More",
                ImageSource = "carousel_flood_history.png",
                IconData = "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z",
                ActionType = "LearnMore"
            },
            new DashboardCarouselItem
            {
                Id = "2",
                Title = "Be Ready Before the Flood",
                Description = "Check your emergency kit, prepare important documents, and review your evacuation plan before heavy rainfall.",
                ButtonText = "View Checklist",
                ImageSource = "carousel_emergency_kit.png",
                IconData = "M20 6h-4V4c0-1.11-.89-2-2-2h-4c-1.11 0-2 .89-2 2v2H4c-1.11 0-1.99.89-1.99 2L2 19c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2zm-6 0h-4V4h4v2z",
                ActionType = "Checklist"
            },
            new DashboardCarouselItem
            {
                Id = "3",
                Title = "AR Safe Route",
                Description = "Use augmented reality to find the safest evacuation route based on live flood levels, weather, and community reports.",
                ButtonText = "Start Navigation",
                ImageSource = "carousel_ar_route.png",
                IconData = "M12 2L4.5 20.29l.71.71L12 18l6.79 3 .71-.71z",
                ActionType = "Camera"
            }
        };

        RefreshDashboard();

        // Listen for real-time admin advisory pushes
        RealtimeAdvisoryManager.OnNewAdvisoryPushed += (newAdvisory) =>
        {
            SelectedAdvisory = newAdvisory;
            IsPopupVisible = true;

            if (newAdvisory != null)
            {
                RiverStatusText = $"Marikina River: {newAdvisory.DisplayAlertLevel}";
                RiverStatusPillText = newAdvisory.DisplayAlertLevel.ToLower() switch
                {
                    "critical" or "high" => "EVACUATE",
                    "warning" or "moderate" => "ALERT",
                    _ => "Monitoring"
                };
            }
        };

        RealtimeAdvisoryManager.StartRealtimeListener();
    }

    public void RefreshDashboard()
    {
        UserName = Preferences.Get("UserName", "Aubrey");
        PreparednessScore = Preferences.Get("PASS_Score", 100);
        ScoreProgress = PreparednessScore / 100.0;
        PreparednessStatus = Preferences.Get("PASS_Status", "Highly Prepared");

        int hour = DateTime.Now.Hour;
        if (hour < 12) Greeting = "Good morning,";
        else if (hour < 18) Greeting = "Good afternoon,";
        else Greeting = "Good evening,";

        // Check if permissions have been requested before
        bool hasRequestedPermissions = Preferences.Get("HasRequestedPermissions", false);
        if (!hasRequestedPermissions)
        {
            IsPermissionsPopupVisible = true;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadUserProfileAsync();
            await LoadOpenMeteoWeatherAsync();
        });
    }

    private async Task LoadUserProfileAsync()
    {
        try
        {
            var client = RescuAR.Services.SupabaseService.Instance.Client;
            if (client != null && client.Auth.CurrentSession != null)
            {
                var authUser = client.Auth.CurrentSession.User;
                Models.User? dbUser = null;
                
                try
                {
                    dbUser = await client.From<Models.User>().Where(x => x.Id == authUser.Id).Single();
                }
                catch { }

                string firstName = string.Empty;

                if (dbUser != null && !string.IsNullOrWhiteSpace(dbUser.FirstName))
                {
                    firstName = dbUser.FirstName;
                }
                else if (authUser.UserMetadata != null && authUser.UserMetadata.TryGetValue("first_name", out var fn))
                {
                    firstName = fn.ToString();
                }

                if (!string.IsNullOrWhiteSpace(firstName))
                {
                    UserName = firstName;
                    Preferences.Set("UserName", firstName);
                }
                else
                {
                    UserName = Preferences.Get("UserName", "RescuAR User");
                }
            }
            else
            {
                UserName = Preferences.Get("UserName", "RescuAR User");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Profile Load Error: {ex.Message}");
            UserName = Preferences.Get("UserName", "RescuAR User");
        }
    }

    private async Task LoadOpenMeteoWeatherAsync()
    {
        try
        {
            double lat = 14.6340; // Default Marikina / QC
            double lon = 121.0990;

            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }
                if (status == PermissionStatus.Granted)
                {
                    var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
                    if (loc != null)
                    {
                        lat = loc.Latitude;
                        lon = loc.Longitude;
                    }
                }
            }
            catch (Exception)
            {
                // Permission fallback
            }

            var weatherData = await _weatherService.GetWeatherAsync(lat, lon);
            if (weatherData != null)
            {
                WeatherTemperatureText = $"{weatherData.TemperatureCelsius} °C";
                WeatherConditionTitle = weatherData.ConditionDescription;
                WeatherConditionSummary = weatherData.ConditionSummary;
                WeatherSummary = $"{weatherData.TemperatureCelsius} °C • {weatherData.ConditionDescription}";

                if (!string.IsNullOrWhiteSpace(weatherData.LocationName))
                {
                    LocationName = weatherData.LocationName;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Open-Meteo Weather Load Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenProfileAsync()
    {
        if (Shell.Current != null)
        {
            try
            {
                await Shell.Current.GoToAsync("ProfilePage");
            }
            catch (Exception)
            {
                // Fallback
            }
        }
    }

    [RelayCommand]
    private async Task OpenPASSAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("Prepare/PASS");
        }
    }

    [RelayCommand]
    private async Task OpenChecklistAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("Prepare/Checklist");
        }
    }

    [RelayCommand]
    private async Task OpenReportsAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//Reports");
        }
    }

    [RelayCommand]
    private async Task OpenHotlinesAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("Prepare/HotlineDirectory");
        }
    }

    [RelayCommand]
    private async Task OpenEvacuationAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("Prepare/EvacuationCenterInfo");
        }
    }

    [RelayCommand]
    private async Task OpenSafetyCircleAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("SafetyCirclePage");
        }
    }

    [RelayCommand]
    private async Task OpenAdvisoriesAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("AdvisoryFeedPage");
        }
    }

    [RelayCommand]
    private void ClosePopup()
    {
        IsPopupVisible = false;
    }

    [RelayCommand]
    private async Task OpenTranslationMenuAsync()
    {
        if (SelectedAdvisory == null || Shell.Current == null) return;

        string result = await Shell.Current.DisplayActionSheetAsync("Translation", "Cancel", null, "English", "Tagalog");
        if (result == "Tagalog")
        {
            SelectedAdvisory.SetLanguage(true);
        }
        else if (result == "English")
        {
            SelectedAdvisory.SetLanguage(false);
        }
    }

    [RelayCommand]
    private async Task OpenCameraAsync()
    {
        if (Shell.Current != null)
        {
            try
            {
                await Shell.Current.GoToAsync("//Camera");
            }
            catch
            {
                await Shell.Current.GoToAsync("CameraPage");
            }
        }
    }

    [RelayCommand]
    private async Task ExecuteCarouselActionAsync(DashboardCarouselItem item)
    {
        if (item == null || Shell.Current == null) return;

        if (item.ActionType == "Checklist")
        {
            await OpenChecklistAsync();
        }
        else if (item.ActionType == "Camera")
        {
            await OpenCameraAsync();
        }
        else if (item.ActionType == "LearnMore")
        {
            await Shell.Current.GoToAsync("Prepare/FloodHistory");
        }
    }

    [RelayCommand]
    private async Task GoToAdvisoriesFeedAsync()
    {
        IsPopupVisible = false;
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("AdvisoryFeedPage");
        }
    }

    [RelayCommand]
    private async Task GrantPermissionsAsync()
    {
        try
        {
            await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            await Permissions.RequestAsync<Permissions.Camera>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error requesting permissions: {ex.Message}");
        }
        finally
        {
            Preferences.Set("HasRequestedPermissions", true);
            IsPermissionsPopupVisible = false;
        }
    }
}
