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
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ScorePercentage { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ButtonText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModuleRoute { get; set; } = "Prepare/PASS";

    [ObservableProperty]
    public partial string ModuleName { get; set; } = string.Empty;

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
        var data = await _dataService.GetPASSDataAsync();
        Title = data.Title;
        ScorePercentage = data.ScorePercentage;
        Description = data.Description;
        ButtonText = data.ButtonText;
        ModuleRoute = "Prepare/PASS";
        ModuleName = data.ModuleName;
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
