using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using RescuAR.App.Services.Dashboard;

namespace RescuAR.App.ViewModels.Dashboard;

public partial class EvacuationOverviewViewModel : ObservableObject
{
    private readonly IDashboardDataService _dataService;

    [ObservableProperty]
    public partial string CenterName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModuleRoute { get; set; } = "Prepare/EvacuationCenterInfo";

    [ObservableProperty]
    public partial string ModuleName { get; set; } = string.Empty;

    public EvacuationOverviewViewModel() : this(DashboardDataService.Instance)
    {
    }

    public EvacuationOverviewViewModel(IDashboardDataService dataService)
    {
        _dataService = dataService;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        var data = await _dataService.GetEvacuationDataAsync();
        CenterName = data.CenterName;
        DistanceText = data.DistanceMeters >= 1000
            ? $"{data.DistanceMeters / 1000:F1} km away"
            : $"{data.DistanceMeters:F0} meters away";
        ActionText = data.ActionText;
        ModuleRoute = "Prepare/EvacuationCenterInfo";
        ModuleName = data.ModuleName;
    }

    [RelayCommand]
    private async Task NavigateToEvacuationCenterAsync()
    {
        if (Shell.Current != null)
        {
            try
            {
                if (Shell.Current.Navigation != null)
                {
                    await Shell.Current.Navigation.PushAsync(new Views.Prepare.EvacuationCenterInfoPage());
                    return;
                }
                await Shell.Current.GoToAsync("Prepare/EvacuationCenterInfo");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }
    }
}
