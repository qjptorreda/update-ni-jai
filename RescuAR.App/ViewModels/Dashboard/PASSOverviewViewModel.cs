using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using RescuAR.App.Services.Dashboard;

namespace RescuAR.App.ViewModels.Dashboard;

public partial class PASSOverviewViewModel : ObservableObject
{
    private readonly IDashboardDataService _dataService;

    [ObservableProperty]
    public partial string Title { get; set; } = "Preparation Assessment";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScoreText))]
    public partial int ScorePercentage { get; set; } = 72;

    public string ScoreText => $"{ScorePercentage}% prepared";

    [ObservableProperty]
    public partial string Description { get; set; } = "Evaluate your overall preparedness for emergencies and evacuation.";

    [ObservableProperty]
    public partial string ButtonText { get; set; } = "Take Assessment";

    [ObservableProperty]
    public partial string ModuleRoute { get; set; } = "Prepare/PASS";

    [ObservableProperty]
    public partial string ModuleName { get; set; } = "Preparation Assessment";

    public PASSOverviewViewModel() : this(DashboardDataService.Instance)
    {
    }

    public PASSOverviewViewModel(IDashboardDataService dataService)
    {
        _dataService = dataService;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            int savedScore = Preferences.Get("PASS_Score", 72);
            ScorePercentage = savedScore;

            var data = await _dataService.GetPASSDataAsync();
            if (data != null)
            {
                if (!string.IsNullOrWhiteSpace(data.Title)) Title = data.Title;
                if (data.ScorePercentage > 0 && savedScore == 72) ScorePercentage = data.ScorePercentage;
                if (!string.IsNullOrWhiteSpace(data.Description)) Description = data.Description;
                if (!string.IsNullOrWhiteSpace(data.ButtonText)) ButtonText = data.ButtonText;
                if (!string.IsNullOrWhiteSpace(data.ModuleName)) ModuleName = data.ModuleName;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PASSOverviewViewModel error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TakeAssessmentAsync()
    {
        if (Shell.Current != null)
        {
            try
            {
                if (Shell.Current.Navigation != null)
                {
                    await Shell.Current.Navigation.PushAsync(new Views.Prepare.PASSPage());
                    return;
                }
                await Shell.Current.GoToAsync("Prepare/PASS");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }
    }
}
