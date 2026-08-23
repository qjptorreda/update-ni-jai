using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using RescuAR.App.Models;
using RescuAR.Services;

namespace RescuAR.App.Services.Reports;

public class AdvisoryService
{
    private Supabase.Client? GetClient()
    {
        return SupabaseService.Instance.Client;
    }

    public async Task<List<DisasterAdvisory>> GetAdvisoriesAsync()
    {
        var client = GetClient();
        if (client != null)
        {
            try
            {
                var response = await client.From<DisasterAdvisory>()
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                if (response.Models != null && response.Models.Count > 0)
                {
                    return response.Models;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supabase Advisory Fetch Error: {ex.Message}");
            }
        }

        return GetFallbackAdvisories();
    }

    private List<DisasterAdvisory> GetFallbackAdvisories()
    {
        return new List<DisasterAdvisory>
        {
            new DisasterAdvisory
            {
                Title = "FLOOD WARNING — Marikina River Level 2",
                Message = "Water level has continuously risen to 16.5 meters. Residents in low-lying areas near Barangay Tumana and Malanday are advised to prepare emergency kits for possible evacuation.",
                WaterLevel = 16.5,
                AlertLevel = "Warning",
                Severity = "High",
                Category = "Flood",
                AffectedArea = "Barangay Tumana / Malanday",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            },
            new DisasterAdvisory
            {
                Title = "Marikina River Alert Level 1 (Standby)",
                Message = "Marikina River water level reached 15.0 meters due to continuous moderate rain upstream. Disaster Response teams are on standby.",
                WaterLevel = 15.0,
                AlertLevel = "Standby",
                Severity = "Low",
                Category = "Flood",
                AffectedArea = "Marikina Riverbanks",
                CreatedAt = DateTime.UtcNow.AddHours(-3)
            }
        };
    }
}

public static class RealtimeAdvisoryManager
{
    private static string? _lastAdvisoryId = null;
    private static readonly AdvisoryService _service = new();
    private static IDispatcherTimer? _timer;
#if ANDROID
    private static Android.Media.MediaPlayer? _activePlayer;
#endif

    public static event Action<DisasterAdvisory>? OnNewAdvisoryPushed;

    public static void StopAlarmAudio()
    {
#if ANDROID
        try
        {
            if (_activePlayer != null)
            {
                if (_activePlayer.IsPlaying)
                {
                    _activePlayer.Stop();
                }
                _activePlayer.Release();
                _activePlayer = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping audio: {ex.Message}");
        }
#endif
    }

    public static void StartRealtimeListener()
    {
        if (_timer != null) return;

        _timer = Application.Current?.Dispatcher.CreateTimer();
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromSeconds(3); // Poll Supabase every 3 seconds for new admin advisories
            _timer.Tick += async (s, e) =>
            {
                try
                {
                    var advisories = await _service.GetAdvisoriesAsync();
                    if (advisories != null && advisories.Count > 0)
                    {
                        var latest = advisories.FirstOrDefault();
                        if (latest != null)
                        {
                            if (_lastAdvisoryId == null)
                            {
                                // Initial startup: record the latest advisory ID so we DO NOT sound the alarm on app launch
                                _lastAdvisoryId = latest.Id;
                            }
                            else if (latest.Id != _lastAdvisoryId)
                            {
                                // Admin pushed a brand new advisory while app is running!
                                _lastAdvisoryId = latest.Id;

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
#if ANDROID
                                    try
                                    {
                                        StopAlarmAudio(); // Stop any existing audio first
                                        var afd = Android.App.Application.Context.Assets?.OpenFd("ndrrmc_alarm.ogg");
                                        if (afd != null)
                                        {
                                            _activePlayer = new Android.Media.MediaPlayer();
                                            _activePlayer.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
                                            _activePlayer.Prepare();
                                            _activePlayer.Start();
                                            
                                            // Release player after it finishes
                                            _activePlayer.Completion += (s, e) => 
                                            { 
                                                try { _activePlayer?.Release(); _activePlayer = null; } catch { }
                                            };
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Audio error: {ex.Message}");
                                    }
#endif
                                    OnNewAdvisoryPushed?.Invoke(latest);
                                });
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore transient network errors
                }
            };
            _timer.Start();
        }
    }
}
