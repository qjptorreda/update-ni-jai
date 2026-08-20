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
    public string BatteryText { get; set; } = "100%";
    public string BatteryIcon { get; set; } = "🔋";
    public Microsoft.Maui.Graphics.Color BatteryColor { get; set; } = Microsoft.Maui.Graphics.Color.FromArgb("#16A34A");
    public bool HasBattery => true;
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

    private readonly System.Collections.Generic.Dictionary<string, byte[]> _avatarRawBytesCache = new();

    private (int percent, bool isCharging) GetRealtimeBatteryLevel()
    {
        int batteryLevel = 0;
        bool isCharging = false;

#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var filter = new Android.Content.IntentFilter(Android.Content.Intent.ActionBatteryChanged);
            var batteryStatus = context.RegisterReceiver(null, filter);
            if (batteryStatus != null)
            {
                int level = batteryStatus.GetIntExtra(Android.OS.BatteryManager.ExtraLevel, -1);
                int scale = batteryStatus.GetIntExtra(Android.OS.BatteryManager.ExtraScale, -1);
                if (level >= 0 && scale > 0)
                {
                    batteryLevel = (int)Math.Round((level / (float)scale) * 100);
                }

                int status = batteryStatus.GetIntExtra(Android.OS.BatteryManager.ExtraStatus, -1);
                isCharging = status == (int)Android.OS.BatteryStatus.Charging || status == (int)Android.OS.BatteryStatus.Full;
            }
        }
        catch { }
#endif

        if (batteryLevel <= 0)
        {
            try
            {
                var charge = Battery.Default.ChargeLevel;
                if (charge >= 0)
                {
                    batteryLevel = (int)Math.Round(charge * 100);
                }
                isCharging = Battery.Default.State == BatteryState.Charging;
            }
            catch { }
        }

        if (batteryLevel <= 0) batteryLevel = 50;

        return (batteryLevel, isCharging);
    }

    private int GenerateLife360PinBitmap(byte[]? avatarBytes, string name, string colorHex, bool isMe, string initials)
    {
        const int width = 140;
        const int height = 175;
        const float circleRadius = 40f;
        const float circleCenterX = width / 2f;
        const float circleCenterY = 48f;

        using var bitmap = new SkiaSharp.SKBitmap(width, height);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        canvas.Clear(SkiaSharp.SKColors.Transparent);

        var pinColor = SkiaSharp.SKColor.Parse(colorHex);

        // 1. Draw Pointer Triangle at bottom of circle pointing down
        using (var trianglePath = new SkiaSharp.SKPath())
        {
            trianglePath.MoveTo(circleCenterX - 14, circleCenterY + circleRadius - 4);
            trianglePath.LineTo(circleCenterX + 14, circleCenterY + circleRadius - 4);
            trianglePath.LineTo(circleCenterX, circleCenterY + circleRadius + 18);
            trianglePath.Close();

            using var trianglePaint = new SkiaSharp.SKPaint
            {
                Color = pinColor,
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Fill
            };
            canvas.DrawPath(trianglePath, trianglePaint);
        }

        // 2. Draw Outer Border Circle
        using (var borderPaint = new SkiaSharp.SKPaint
        {
            Color = pinColor,
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill
        })
        {
            canvas.DrawCircle(circleCenterX, circleCenterY, circleRadius, borderPaint);
        }

        // 3. Draw Inner White Ring
        using (var whiteRingPaint = new SkiaSharp.SKPaint
        {
            Color = SkiaSharp.SKColors.White,
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill
        })
        {
            canvas.DrawCircle(circleCenterX, circleCenterY, circleRadius - 4, whiteRingPaint);
        }

        // 4. Draw Avatar Image or Initials
        float innerRadius = circleRadius - 6;
        bool drewAvatar = false;
        if (avatarBytes != null && avatarBytes.Length > 0)
        {
            try
            {
                using var origBitmap = SkiaSharp.SKBitmap.Decode(avatarBytes);
                if (origBitmap != null)
                {
                    using var shader = SkiaSharp.SKShader.CreateBitmap(
                        origBitmap,
                        SkiaSharp.SKShaderTileMode.Clamp,
                        SkiaSharp.SKShaderTileMode.Clamp,
                        SkiaSharp.SKMatrix.CreateScale(
                            (innerRadius * 2f) / origBitmap.Width,
                            (innerRadius * 2f) / origBitmap.Height
                        ).PostConcat(SkiaSharp.SKMatrix.CreateTranslation(circleCenterX - innerRadius, circleCenterY - innerRadius))
                    );

                    using var avatarPaint = new SkiaSharp.SKPaint
                    {
                        Shader = shader,
                        IsAntialias = true
                    };
                    canvas.DrawCircle(circleCenterX, circleCenterY, innerRadius, avatarPaint);
                    drewAvatar = true;
                }
            }
            catch { }
        }

        if (!drewAvatar)
        {
            // Draw Initials with colored background
            using var initBgPaint = new SkiaSharp.SKPaint
            {
                Color = pinColor,
                IsAntialias = true,
                Style = SkiaSharp.SKPaintStyle.Fill
            };
            canvas.DrawCircle(circleCenterX, circleCenterY, innerRadius, initBgPaint);

            using var textPaint = new SkiaSharp.SKPaint
            {
                Color = SkiaSharp.SKColors.White,
                TextSize = 24,
                IsAntialias = true,
                TextAlign = SkiaSharp.SKTextAlign.Center,
                Typeface = SkiaSharp.SKTypeface.FromFamilyName("sans-serif", SkiaSharp.SKFontStyle.Bold)
            };
            canvas.DrawText(initials, circleCenterX, circleCenterY + 9, textPaint);
        }

        // 5. Draw Name Pill Tag at the bottom
        string displayName = isMe ? "Me now" : (name.Split(' ')[0]);
        float pillY = circleCenterY + circleRadius + 22;
        float pillHeight = 26;
        
        using var pillTextPaint = new SkiaSharp.SKPaint
        {
            Color = SkiaSharp.SKColors.White,
            TextSize = 16,
            IsAntialias = true,
            TextAlign = SkiaSharp.SKTextAlign.Center,
            Typeface = SkiaSharp.SKTypeface.FromFamilyName("sans-serif", SkiaSharp.SKFontStyle.Bold)
        };

        float textWidth = pillTextPaint.MeasureText(displayName);
        float pillWidth = Math.Max(70, textWidth + 24);
        float pillX = circleCenterX - (pillWidth / 2f);

        using (var pillPaint = new SkiaSharp.SKPaint
        {
            Color = SkiaSharp.SKColor.Parse("#1E293B"),
            IsAntialias = true,
            Style = SkiaSharp.SKPaintStyle.Fill
        })
        {
            var roundRect = new SkiaSharp.SKRoundRect(new SkiaSharp.SKRect(pillX, pillY, pillX + pillWidth, pillY + pillHeight), 13, 13);
            canvas.DrawRoundRect(roundRect, pillPaint);
        }

        canvas.DrawText(displayName, circleCenterX, pillY + 19, pillTextPaint);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var ms = new MemoryStream();
        data.SaveTo(ms);
        return Mapsui.Styles.BitmapRegistry.Instance.Register(ms.ToArray());
    }

    private async Task PollLocationsAsync()
    {
        try
        {
            // 1. Push our own location with real-time battery
            var (myBatteryPercent, myIsCharging) = GetRealtimeBatteryLevel();
            string myBatteryStatus = $"{myBatteryPercent}%{(myIsCharging ? "⚡" : "")}";

            var hasPermission = await CheckAndRequestLocationPermission();
            if (hasPermission)
            {
                var loc = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(3)));
                if (loc != null)
                {
                    string pushStatus = $"Online|{myBatteryStatus}";
                    await _safetyCircleService.PushLocationAsync(loc.Latitude, loc.Longitude, pushStatus);
                    
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
            string currentUserId = string.Empty;
            try { currentUserId = _safetyCircleService.GetCurrentUserId(); } catch { }

            CircleMembers.Clear();
            foreach (var member in members)
            {
                var userLoc = locations.FirstOrDefault(l => string.Equals(l.UserId, member.Id, StringComparison.OrdinalIgnoreCase));
                string rawStatus = userLoc?.StatusText ?? "Online";
                string displayStatus = "Online";
                string batteryText = string.Empty;
                string batteryIcon = "🔋";
                var batteryColor = Microsoft.Maui.Graphics.Color.FromArgb("#16A34A");

                if (rawStatus.Contains("|"))
                {
                    var parts = rawStatus.Split('|');
                    displayStatus = parts[0];
                    batteryText = parts[1];
                    if (batteryText.Contains("⚡"))
                    {
                        batteryIcon = "⚡";
                        batteryColor = Microsoft.Maui.Graphics.Color.FromArgb("#2563EB");
                    }
                    else
                    {
                        var cleanVal = batteryText.Replace("%", "").Trim();
                        if (int.TryParse(cleanVal, out int bVal))
                        {
                            if (bVal <= 20) batteryColor = Microsoft.Maui.Graphics.Color.FromArgb("#EF4444");
                            else if (bVal <= 50) batteryColor = Microsoft.Maui.Graphics.Color.FromArgb("#F59E0B");
                            else batteryColor = Microsoft.Maui.Graphics.Color.FromArgb("#16A34A");
                        }
                    }
                }

                bool isMe = string.Equals(member.Id, currentUserId, StringComparison.OrdinalIgnoreCase);
                if (isMe || string.IsNullOrEmpty(batteryText))
                {
                    batteryText = $"{myBatteryPercent}%";
                    batteryIcon = myIsCharging ? "⚡" : "🔋";
                    batteryColor = myIsCharging
                        ? Microsoft.Maui.Graphics.Color.FromArgb("#2563EB")
                        : (myBatteryPercent <= 20 ? Microsoft.Maui.Graphics.Color.FromArgb("#EF4444")
                        : (myBatteryPercent <= 50 ? Microsoft.Maui.Graphics.Color.FromArgb("#F59E0B")
                        : Microsoft.Maui.Graphics.Color.FromArgb("#16A34A")));
                }

                var cm = new CircleMember
                {
                    Name = $"{member.FirstName} {member.LastName}".Trim(),
                    Initials = (member.FirstName?.Length > 0 ? member.FirstName.Substring(0, 1) : "") + (member.LastName?.Length > 0 ? member.LastName.Substring(0, 1) : ""),
                    StatusText = displayStatus,
                    BatteryText = batteryText,
                    BatteryIcon = batteryIcon,
                    BatteryColor = batteryColor,
                    Latitude = userLoc?.Latitude ?? 0,
                    Longitude = userLoc?.Longitude ?? 0,
                    ColorTheme = GetColorForUser(member.Id),
                    AvatarUrl = member.AvatarUrl
                };

                // Fetch avatar bytes for SkiaSharp Life360 pin rendering
                byte[]? avatarBytes = null;
                if (!string.IsNullOrEmpty(cm.AvatarUrl))
                {
                    if (!_avatarRawBytesCache.TryGetValue(member.Id, out avatarBytes))
                    {
                        try
                        {
                            avatarBytes = await _httpClient.GetByteArrayAsync(cm.AvatarUrl);
                            _avatarRawBytesCache[member.Id] = avatarBytes;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to download avatar for {member.Id}: {ex.Message}");
                        }
                    }
                }

                string pinColorHex = isMe ? "#10B981" : cm.ColorTheme.ToHex();
                int pinBitmapId = GenerateLife360PinBitmap(avatarBytes, cm.Name, pinColorHex, isMe, cm.Initials);
                cm.AvatarBitmapId = pinBitmapId;

                CircleMembers.Add(cm);
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
        foreach (var member in CircleMembers.Where(m => m.Latitude != 0 && m.Longitude != 0))
        {
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(member.Longitude, member.Latitude);
            
            // Halo (Translucent Outer Ring)
            var haloFeature = new Mapsui.Nts.GeometryFeature(new NetTopologySuite.Geometries.Point(x, y));
            var colorTheme = member.ColorTheme;
            var translucentColor = new Mapsui.Styles.Color((int)(colorTheme.Red * 255), (int)(colorTheme.Green * 255), (int)(colorTheme.Blue * 255), 40);
            haloFeature.Styles.Add(new Mapsui.Styles.SymbolStyle
            {
                SymbolType = Mapsui.Styles.SymbolType.Ellipse,
                SymbolScale = 1.2,
                Fill = new Mapsui.Styles.Brush(translucentColor),
                Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.Transparent)
            });
            features.Add(haloFeature);

            // Inner Pin (Life360 Avatar + Pointer + Name Pill)
            var feature = new Mapsui.Nts.GeometryFeature(new NetTopologySuite.Geometries.Point(x, y));
            
            if (member.AvatarBitmapId.HasValue)
            {
                feature.Styles.Add(new Mapsui.Styles.SymbolStyle
                {
                    BitmapId = member.AvatarBitmapId.Value,
                    SymbolScale = 0.5,
                    SymbolOffset = new Mapsui.Styles.Offset(0, 35)
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
        map.Refresh();
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

    [RelayCommand]
    private async Task OpenChatAsync()
    {
        if (string.IsNullOrEmpty(_currentCircleId))
        {
            if (Shell.Current != null)
                await Shell.Current.DisplayAlert("Select Circle", "Please create or select a Safety Circle first to chat with family members.", "OK");
            return;
        }

        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync($"CircleChatPage?circleId={_currentCircleId}&circleName={Uri.EscapeDataString(SelectedCircleName)}");
        }
    }
}
