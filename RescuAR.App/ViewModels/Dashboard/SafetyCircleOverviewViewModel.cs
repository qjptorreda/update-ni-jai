using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using RescuAR.App.Services.Dashboard;

namespace RescuAR.App.ViewModels.Dashboard;

public partial class SafetyCircleOverviewViewModel : ObservableObject
{
    private readonly IDashboardDataService _dataService;

    public ObservableCollection<SafetyCircleGroupItem> Groups { get; } = new();

    [ObservableProperty]
    public partial string ActionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ModuleRoute { get; set; } = "//Map";

    [ObservableProperty]
    public partial string ModuleName { get; set; } = string.Empty;

    public SafetyCircleOverviewViewModel() : this(DashboardDataService.Instance)
    {
    }

    public SafetyCircleOverviewViewModel(IDashboardDataService dataService)
    {
        _dataService = dataService;
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        var data = await _dataService.GetSafetyCircleDataAsync();
        Groups.Clear();
        foreach (var group in data.Groups)
        {
            Groups.Add(group);
        }
        ActionText = data.ActionText;
        ModuleRoute = "//Map";
        ModuleName = data.ModuleName;
    }

    [RelayCommand]
    private async Task NavigateToSafetyCircleAsync()
    {
        if (Shell.Current != null)
        {
            try
            {
                await Shell.Current.GoToAsync("//Map");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }
    }
}
