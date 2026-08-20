using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Mapsui;
using Mapsui.UI.Maui;
using Mapsui.Nts;
using System.Linq;
using NetTopologySuite.Geometries;
using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using RescuAR.App.Models;

namespace RescuAR.App.ViewModels.Map;

public class CircleMember
{
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public Microsoft.Maui.Graphics.Color ColorTheme { get; set; } = Microsoft.Maui.Graphics.Colors.Teal;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string AvatarUrl { get; set; } = string.Empty;
    public int? AvatarBitmapId { get; set; }
}

public partial class SafetyCircleViewModel : ObservableObject
{
    [ObservableProperty]
    private Mapsui.Map _map = new();

    [ObservableProperty]
    private string _selectedCircleName = "Select a Circle";

    [ObservableProperty]
    private bool _isPeopleSheetOpen = false;

    [ObservableProperty]
    private bool _isCircleDropdownOpen = false;

    public ObservableCollection<CircleMember> CircleMembers { get; } = new();
    public ObservableCollection<SupabaseSafetyCircle> MyCircles { get; } = new();

    private Mapsui.Layers.MemoryLayer _pinsLayer;
    private readonly RescuAR.App.Services.Cloud.SafetyCircleService _safetyCircleService;
    private IDispatcherTimer _locationTimer;
    private string _currentCircleId = "";
    private bool _hasCenteredOnUser = false;
    
    // Cache for downloaded avatars to map to Mapsui BitmapRegistry IDs
    private readonly System.Collections.Generic.Dictionary<string, int> _avatarBitmapCache = new();
    private readonly System.Net.Http.HttpClient _httpClient = new();

    public SafetyCircleViewModel(RescuAR.App.Services.Cloud.SafetyCircleService safetyCircleService)
    {
        _safetyCircleService = safetyCircleService;

        _locationTimer = Application.Current.Dispatcher.CreateTimer();
        _locationTimer.Interval = TimeSpan.FromSeconds(5);
        _locationTimer.Tick += async (s, e) => await PollLocationsAsync();
    }

    public async Task LoadMyCirclesAsync()
    {
        try
        {
            var circles = await _safetyCircleService.GetMyCirclesAsync();
            MyCircles.Clear();
            foreach (var circle in circles)
            {
                MyCircles.Add(circle);
            }

            if (MyCircles.Any())
            {
                // If no circle selected or previously selected circle not in list, select first
                if (string.IsNullOrEmpty(_currentCircleId) || !MyCircles.Any(c => c.Id == _currentCircleId))
                {
                    SelectCircle(MyCircles.First());
                }
            }
            else
            {
                SelectedCircleName = "No Circles Joined";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading circles: {ex.Message}");
        }
    }

    public void SelectCircle(SupabaseSafetyCircle circle)
    {
        _currentCircleId = circle.Id;
        SelectedCircleName = circle.Name;
        IsCircleDropdownOpen = false;
        
        // Load members instantly, then timer will keep updating
        _ = PollLocationsAsync();
        _locationTimer.Start();
    }

    private async Task<bool> CheckAndRequestLocationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
            return true;
        
        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status == PermissionStatus.Granted;
    }

    private async Task PollLocationsAsync()
    {
        try
        {
            // 1. Push our own location (if we have permission)
            var hasPermission = await CheckAndRequestLocationPermission();
            if (hasPermission)
            {
                var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3)));
                if (loc != null)
                {
                    await _safetyCircleService.PushLocationAsync(loc.Latitude, loc.Longitude, "Online");
                    
                    // Auto-center map on user's current GPS location on first fix
                    if (!_hasCenteredOnUser && Map != null)
                    {
                        _hasCenteredOnUser = true;
                        var (userX, userY) = Mapsui.Projections.SphericalMercator.FromLonLat(loc.Longitude, loc.Latitude);
                        Map.Navigator?.CenterOnAndZoomTo(new MPoint(userX, userY), 25);
                    }
                }
            }

            if (string.IsNullOrEmpty(_currentCircleId)) return;

            // 2. Pull other members
            var members = await _safetyCircleService.GetCircleMembersAsync(_currentCircleId);
            var locations = await _safetyCircleService.GetCircleLocationsAsync(_currentCircleId);

            CircleMembers.Clear();
            foreach (var member in members)
            {
                var userLoc = locations.FirstOrDefault(l => l.UserId == member.Id);
                var cm = new CircleMember
                {
                    Name = $"{member.FirstName} {member.LastName}".Trim(),
                    Initials = (member.FirstName?.Length > 0 ? member.FirstName.Substring(0, 1) : "") + (member.LastName?.Length > 0 ? member.LastName.Substring(0, 1) : ""),
                    StatusText = userLoc?.StatusText ?? "Offline",
                    Latitude = userLoc?.Latitude ?? 0,
                    Longitude = userLoc?.Longitude ?? 0,
                    ColorTheme = GetColorForUser(member.Id),
                    AvatarUrl = member.AvatarUrl
                };

                // Download and register bitmap for Mapsui if available and not cached
                if (!string.IsNullOrEmpty(cm.AvatarUrl))
                {
                    if (!_avatarBitmapCache.ContainsKey(member.Id))
                    {
                        try
                        {
                            var imageBytes = await _httpClient.GetByteArrayAsync(cm.AvatarUrl);
                            int bitmapId = Mapsui.Styles.BitmapRegistry.Instance.Register(imageBytes);
                            _avatarBitmapCache[member.Id] = bitmapId;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to download avatar for {member.Id}: {ex.Message}");
                        }
                    }

                    if (_avatarBitmapCache.TryGetValue(member.Id, out int cachedBitmapId))
                    {
                        cm.AvatarBitmapId = cachedBitmapId;
                    }
                }
                
                // Only show on map if they have coordinates
                if (cm.Latitude != 0 && cm.Longitude != 0)
                {
                    CircleMembers.Add(cm);
                }
            }

            if (Map != null)
            {
                UpdateMapMarkers(Map);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Polling Error: {ex.Message}");
        }
    }

    private Microsoft.Maui.Graphics.Color GetColorForUser(string userId)
    {
        int hash = userId.GetHashCode();
        var colors = new[] { "#0A8491", "#EAB308", "#931492", "#E11D48", "#2563EB", "#16A34A" };
        return Microsoft.Maui.Graphics.Color.FromArgb(colors[Math.Abs(hash) % colors.Length]);
    }

    public async Task InitializeMapAsync(MapControl mapControl)
    {
        try
        {
            var map = new Mapsui.Map
            {
                CRS = "EPSG:3857"
            };

            // Load Online OpenStreetMap Base Layer
            map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());

            // Center and Zoom to Map Data (Default Marikina or user location)
            var (homeX, homeY) = Mapsui.Projections.SphericalMercator.FromLonLat(121.1029, 14.6507);
            map.Home = n => n.CenterOnAndZoomTo(new MPoint(homeX, homeY), 38.2);

            Map = map;
            mapControl.Map = map;

            // Draw initial pins
            UpdateMapMarkers(map);
        }
        catch (Exception ex)
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Map Error", $"Base Map failed: {ex.Message}", "OK");
            Console.WriteLine($"Error loading map: {ex.Message}");
        }
    }

    private void UpdateMapMarkers(Mapsui.Map map)
    {
        var features = new System.Collections.Generic.List<Mapsui.Nts.GeometryFeature>();

        // Safety Circle Member Pins
        foreach (var member in CircleMembers)
        {
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(member.Longitude, member.Latitude);
            
            // Halo (Translucent Outer Ring)
            var haloFeature = new Mapsui.Nts.GeometryFeature(new NetTopologySuite.Geometries.Point(x, y));
            var colorTheme = member.ColorTheme;
            var translucentColor = new Mapsui.Styles.Color((int)(colorTheme.Red * 255), (int)(colorTheme.Green * 255), (int)(colorTheme.Blue * 255), 40); // 40 alpha = ~15% opacity
            haloFeature.Styles.Add(new Mapsui.Styles.SymbolStyle
            {
                SymbolType = Mapsui.Styles.SymbolType.Ellipse,
                SymbolScale = 1.2, // Larger than pin
                Fill = new Mapsui.Styles.Brush(translucentColor),
                Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.Transparent)
            });
            features.Add(haloFeature);

            // Inner Pin (Dot or Avatar)
            var feature = new Mapsui.Nts.GeometryFeature(new NetTopologySuite.Geometries.Point(x, y));
            
            if (member.AvatarBitmapId.HasValue)
            {
                feature.Styles.Add(new Mapsui.Styles.SymbolStyle
                {
                    BitmapId = member.AvatarBitmapId.Value,
                    SymbolScale = 0.25
                });
            }
            else
            {
                feature.Styles.Add(new Mapsui.Styles.SymbolStyle
                {
                    SymbolType = Mapsui.Styles.SymbolType.Ellipse,
                    SymbolScale = 0.5,
                    Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.FromString(member.ColorTheme.ToHex())), 
                    Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 3)
                });
            }
            features.Add(feature);
        }

        var oldLayer = map.Layers.FirstOrDefault(l => l.Name == "MapPins");
        if (oldLayer != null)
        {
            map.Layers.Remove(oldLayer);
        }

        _pinsLayer = new Mapsui.Layers.MemoryLayer
        {
            Name = "MapPins",
            Features = features
        };

        map.Layers.Add(_pinsLayer);
    }

    [RelayCommand]
    private void TogglePeopleSheet()
    {
        IsPeopleSheetOpen = !IsPeopleSheetOpen;
        if (IsPeopleSheetOpen) IsCircleDropdownOpen = false;
    }

    [RelayCommand]
    private void ToggleCircleDropdown()
    {
        IsCircleDropdownOpen = !IsCircleDropdownOpen;
        if (IsCircleDropdownOpen) IsPeopleSheetOpen = false;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Map?.Navigator?.ZoomIn();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Map?.Navigator?.ZoomOut();
    }

    [RelayCommand]
    private void ZoomReset()
    {
        var (homeX, homeY) = Mapsui.Projections.SphericalMercator.FromLonLat(121.1029, 14.6507);
        Map?.Navigator?.CenterOnAndZoomTo(new MPoint(homeX, homeY), 38.2);
    }

    [RelayCommand]
    private async Task CreateCircleAsync()
    {
        if (Shell.Current == null) return;
        string result = await Shell.Current.DisplayPromptAsync("Create Circle", "Enter a name for your new Safety Circle:", "Create", "Cancel");
        if (!string.IsNullOrWhiteSpace(result))
        {
            try
            {
                var circle = await _safetyCircleService.CreateCircleAsync(result);
                await Shell.Current.DisplayAlert("Circle Created!", $"Your invite code is: {circle.InviteCode}\nShare this with your family/friends.", "OK");
                await LoadMyCirclesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }

    [RelayCommand]
    private async Task JoinCircleAsync()
    {
        if (Shell.Current == null) return;
        string result = await Shell.Current.DisplayPromptAsync("Join Circle", "Enter the 6-character Invite Code:", "Join", "Cancel");
        if (!string.IsNullOrWhiteSpace(result))
        {
            try
            {
                var circle = await _safetyCircleService.JoinCircleWithCodeAsync(result);
                await Shell.Current.DisplayAlert("Success", $"You've joined {circle.Name}!", "OK");
                await LoadMyCirclesAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }

    [RelayCommand]
    private void SelectCircleCommand(RescuAR.App.Models.SupabaseSafetyCircle circle)
    {
        if (circle != null)
        {
            SelectCircle(circle);
        }
    }
}
