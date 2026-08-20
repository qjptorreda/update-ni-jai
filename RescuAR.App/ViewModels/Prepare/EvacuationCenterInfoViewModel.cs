using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Controls;

namespace RescuAR.App.ViewModels.Prepare;

public class EmergencyHotlineItem
{
    public string Name { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string BadgeText { get; set; } = "EMS";
}

public partial class EvacuationCenterItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Distance { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public string Address { get; set; } = string.Empty;
    public string VerifiedBy { get; set; } = string.Empty;
    public string FacilityImageUrl { get; set; } = string.Empty;
    public string MapImageSource { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public bool HasFacilityImage => !string.IsNullOrWhiteSpace(FacilityImageUrl);

    public string Status { get; set; } = "Open";

    public string StatusPillBg => "#DCFCE7";

    public string StatusPillText => "#16A34A";

    public string StatusLabel => "Open / Operational";
}

public partial class EvacuationCenterInfoViewModel : ObservableObject
{
    public ObservableCollection<EmergencyHotlineItem> Hotlines { get; } = new();
    public ObservableCollection<EvacuationCenterItem> EvacuationCenters { get; } = new();

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "Nearest";

    public EvacuationCenterInfoViewModel()
    {
        LoadData();
        _ = FilterEvacuationCentersByGpsAsync();
    }

    private void LoadData()
    {
        Hotlines.Clear();
        Hotlines.Add(new EmergencyHotlineItem { Name = "Marikina Rescue 161", Number = "(02) 161", Type = "24/7 Emergency Medical & Rescue", BadgeText = "EMS" });
        Hotlines.Add(new EmergencyHotlineItem { Name = "Marikina PNP Central", Number = "(02) 8405-0091", Type = "Police Emergency Hotline", BadgeText = "PNP" });
        Hotlines.Add(new EmergencyHotlineItem { Name = "Marikina BFP Fire Dept", Number = "(02) 8646-0427", Type = "Fire & Rescue Brigade", BadgeText = "BFP" });
        Hotlines.Add(new EmergencyHotlineItem { Name = "Red Cross Marikina", Number = "(02) 8681-3442", Type = "Disaster Relief & Blood Bank", BadgeText = "PRC" });

        // Load all available candidate centers
        PopulateMasterCenters(14.6612, 121.0963);
    }

    private async Task FilterEvacuationCentersByGpsAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            if (location == null)
            {
                location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3)));
            }

            double userLat = location != null ? location.Latitude : 14.6612;
            double userLng = location != null ? location.Longitude : 121.0963;

            PopulateMasterCenters(userLat, userLng);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GPS location error: {ex.Message}");
        }
    }

    private void PopulateMasterCenters(double userLat, double userLng)
    {
        var masterList = new List<EvacuationCenterItem>
        {
            new EvacuationCenterItem 
            { 
                Name = "Malanday Elementary School", 
                Address = "48 Visayas St., Malanday\nMarikina City 1805", 
                VerifiedBy = "Marikina LGU",
                Latitude = 14.6612,
                Longitude = 121.0963,
                FacilityImageUrl = "https://pbs.twimg.com/media/Emm23rQVQAAbUe3?format=jpg&name=large",
                MapImageSource = "https://staticmap.openstreetmap.de/staticmap.php?center=14.6612,121.0963&zoom=16&size=600x300&markers=14.6612,121.0963,red-pushpin"
            },
            new EvacuationCenterItem 
            { 
                Name = "San Roque High School Evacuation Facility", 
                Address = "Abad Santos St., San Roque\nMarikina City 1801", 
                VerifiedBy = "Marikina LGU",
                Latitude = 14.6258,
                Longitude = 121.1042,
                FacilityImageUrl = "https://www.airesingegneria.it/site/assets/files/1208/metro-manila-edifici.jpg",
                MapImageSource = "https://staticmap.openstreetmap.de/staticmap.php?center=14.6258,121.1042&zoom=16&size=600x300&markers=14.6258,121.1042,red-pushpin"
            },
            new EvacuationCenterItem 
            { 
                Name = "Concepcion Uno Covered Court", 
                Address = "J.P. Rizal St., Concepcion Uno\nMarikina City 1807", 
                VerifiedBy = "Red Cross PH Verified",
                Latitude = 14.6521,
                Longitude = 121.1084,
                FacilityImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d5/Barangay_Concepcion_Uno%2C_Marikina_City_%28Rizal%2C_Metro_Manila%3B_2023-08-07%29_E911a_22.jpg/3840px-Barangay_Concepcion_Uno%2C_Marikina_City_%28Rizal%2C_Metro_Manila%3B_2023-08-07%29_E911a_22.jpg",
                MapImageSource = "https://staticmap.openstreetmap.de/staticmap.php?center=14.6521,121.1084&zoom=16&size=600x300&markers=14.6521,121.1084,red-pushpin"
            },
            new EvacuationCenterItem 
            { 
                Name = "Marikina Elementary School", 
                Address = "W.C. Paz St., Sta. Elena\nMarikina City 1800", 
                VerifiedBy = "Marikina LGU",
                Latitude = 14.6335,
                Longitude = 121.0968,
                FacilityImageUrl = "https://blogger.googleusercontent.com/img/b/R29vZ2xl/AVvXsEgfm1_L35DOsZzEP6Op7KXLa44OxSrijBZ3zuIF4bczTgTQvA4c2GWNhzxlmy1UqFaZz47_IyXrAWuM6zZv8CDTR7ZwVITldWURKjINOxGi94kvfhRuN5mXYWT3geYrG3KJmemaYDL7hKc/w1200-h630-p-k-no-nu/2018-02-25_05.54.17_1%255B1%255D.jpg",
                MapImageSource = "https://staticmap.openstreetmap.de/staticmap.php?center=14.6335,121.0968&zoom=16&size=600x300&markers=14.6335,121.0968,red-pushpin"
            }
        };

        var userLoc = new Location(userLat, userLng);

        foreach (var center in masterList)
        {
            var centerLoc = new Location(center.Latitude, center.Longitude);
            double distKm = Location.CalculateDistance(userLoc, centerLoc, DistanceUnits.Kilometers);
            center.DistanceKm = distKm;
            center.Distance = distKm < 1.0 ? $"{Math.Round(distKm * 1000)} meters away" : $"{distKm:F1} km away";
        }

        // Filter strictly for nearby evacuation centers (within 2.5 km of user's GPS location) and sort by distance
        var nearbyCenters = masterList
            .Where(c => c.DistanceKm <= 2.5)
            .OrderBy(c => c.DistanceKm)
            .ToList();

        // Fallback: If no center is within 2.5 km, show top 2 closest centers
        if (nearbyCenters.Count == 0)
        {
            nearbyCenters = masterList.OrderBy(c => c.DistanceKm).Take(2).ToList();
        }

        EvacuationCenters.Clear();
        foreach (var item in nearbyCenters)
        {
            EvacuationCenters.Add(item);
        }
    }

    [RelayCommand]
    private async Task MakePhoneCall(string number)
    {
        if (string.IsNullOrWhiteSpace(number)) return;

        try
        {
            // Clean phone number format for direct dialer input
            string cleanDigits = System.Text.RegularExpressions.Regex.Replace(number, @"[^\d+]", "");
            if (!string.IsNullOrWhiteSpace(cleanDigits))
            {
                var uri = new Uri($"tel:{cleanDigits}");
                await Launcher.Default.OpenAsync(uri);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Phone call error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ViewMoreDetailsAsync(EvacuationCenterItem center)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//Map");
        }
    }

    [RelayCommand]
    private async Task NavigateToMapAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//Map");
        }
    }
}
