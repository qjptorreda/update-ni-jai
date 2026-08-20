using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace RescuAR.App.ViewModels.Prepare;

public partial class DocumentaryVideosViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string OndoyVideoUrl { get; set; } = "https://www.youtube.com/embed/l_Bf5alw3Ps";

    [ObservableProperty]
    public partial string UlyssesVideoUrl { get; set; } = "https://www.youtube.com/embed/Qq_nTQXzpmo";

    [RelayCommand]
    private async Task BackAsync()
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
