using System.Text.Json;
using Android.Content;
using Android.Util;

using RescuAR.App.Models;
using RescuAR.App.Services.Unity;

namespace RescuAR.App.Platforms.Android.Unity;

public sealed class UnityService : IUnityService
{
    public void LaunchUnity(EvacuationCenter center)
    {
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;

            if (activity == null)
            {
                Log.Error("RESCUAR_UAAL", "CurrentActivity is null. Cannot launch Unity.");
                return;
            }

            var intent = new Intent();
            // UAAL approach: specify the package and the Unity player activity class
            intent.SetClassName(
                "com.rescuar.augmentedreality",
                "com.unity3d.player.UnityPlayerGameActivity");

            string json = JsonSerializer.Serialize(center);

            intent.PutExtra("evacuation_center", json);
            intent.AddFlags(ActivityFlags.SingleTop);

            activity.StartActivity(intent);

            Log.Debug("RESCUAR_UAAL", "Unity application launched successfully.");
        }
        catch (Exception ex)
        {
            Log.Error("RESCUAR_UAAL", $"Failed to launch Unity application: {ex}");
        }
    }
}
