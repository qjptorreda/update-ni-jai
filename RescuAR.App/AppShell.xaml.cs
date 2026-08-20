namespace RescuAR.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("Prepare/Checklist", typeof(Views.Prepare.ChecklistPage));
        Routing.RegisterRoute("Prepare/PASS", typeof(Views.Prepare.PASSPage));
        Routing.RegisterRoute("Prepare/Assessment", typeof(Views.Prepare.AssessmentPage));
        Routing.RegisterRoute("Prepare/EvacuationCenterInfo", typeof(Views.Prepare.EvacuationCenterInfoPage));
        Routing.RegisterRoute("Prepare/HotlineDirectory", typeof(Views.Prepare.HotlineDirectoryPage));
        Routing.RegisterRoute("AdvisoryFeedPage", typeof(Views.Reports.AdvisoryFeedPage));
        Routing.RegisterRoute("Reports/AdvisoryFeed", typeof(Views.Reports.AdvisoryFeedPage));
        Routing.RegisterRoute("ProfilePage", typeof(Views.Profile.ProfilePage));
        Routing.RegisterRoute("PersonalInformationPage", typeof(Views.Profile.PersonalInformationPage));
        Routing.RegisterRoute("HealthInformationPage", typeof(Views.Profile.HealthInformationPage));
        Routing.RegisterRoute("SafetyCircleSettingsPage", typeof(Views.Profile.SafetyCircleSettingsPage));
        Routing.RegisterRoute("EmergencyContactsPage", typeof(Views.Profile.EmergencyContactsPage));
        Routing.RegisterRoute("AppSettingsPage", typeof(Views.Profile.AppSettingsPage));
        Routing.RegisterRoute("HelpCenterPage", typeof(Views.Profile.HelpCenterPage));
        Routing.RegisterRoute("PrivacyPolicyPage", typeof(Views.Profile.PrivacyPolicyPage));
        Routing.RegisterRoute("TermsConditionsPage", typeof(Views.Profile.TermsConditionsPage));
        Routing.RegisterRoute("SystemInformationPage", typeof(Views.Profile.SystemInformationPage));
        
        // Routing.RegisterRoute("Reports/CommunityPosting", typeof(Views.Reports.CommunityPostingPage));
        Routing.RegisterRoute(nameof(RescuAR.App.Views.Map.CircleChatPage), typeof(RescuAR.App.Views.Map.CircleChatPage));
        Routing.RegisterRoute("ReportDetails", typeof(Views.Reports.ReportDetailsPage));
        Routing.RegisterRoute("SafetyCircleOverviewPage", typeof(Views.Dashboard.SafetyCircleOverviewPage));
        Routing.RegisterRoute("SafetyCirclePage", typeof(Views.Map.SafetyCirclePage));
        Routing.RegisterRoute("SummaryPage", typeof(Views.Summary.SummaryPage));
        Routing.RegisterRoute("CameraPage", typeof(Views.Camera.CameraPage));
        Routing.RegisterRoute("NotificationsPage", typeof(Views.Reports.NotificationsPage));
        Routing.RegisterRoute("Prepare/FloodHistory", typeof(Views.Prepare.FloodHistoryPage));
        Routing.RegisterRoute("FloodHistoryPage", typeof(Views.Prepare.FloodHistoryPage));
        Routing.RegisterRoute("Prepare/HistoricalPhotos", typeof(Views.Prepare.HistoricalPhotosPage));
        Routing.RegisterRoute("Prepare/DocumentaryVideos", typeof(Views.Prepare.DocumentaryVideosPage));
        Routing.RegisterRoute("Prepare/FloodTimeline", typeof(Views.Prepare.FloodTimelinePage));
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);

        // Safety: If the shell is not yet fully initialized/constructed, skip checking to prevent startup crash
        if (Shell.Current == null)
            return;

        // When navigating to the Home tab, if we are currently inside a sub-page, pop back to the dashboard root.
        if (args.Target?.Location?.OriginalString.Contains("Home") == true)
        {
            try
            {
                if (Navigation?.NavigationStack != null && Navigation.NavigationStack.Count > 1)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await Navigation.PopToRootAsync();
                        }
                        catch
                        {
                            // Ignore
                        }
                    });
                }
            }
            catch
            {
                // Ignore any uninitialized navigation stack access exceptions
            }
        }
    }
}
