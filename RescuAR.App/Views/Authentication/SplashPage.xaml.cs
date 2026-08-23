using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using RescuAR.App.ViewModels.Authentication;

namespace RescuAR.App.Views.Authentication;

public partial class SplashPage : ContentPage
{
    private CancellationTokenSource? _animationCts;

    public SplashPage(SplashViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _animationCts = new CancellationTokenSource();

        // Run the 3-second programmatic AR evacuation micro-animation loop
        _ = RunArEvacuationAnimationAsync(_animationCts.Token);

        if (BindingContext is SplashViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _animationCts?.Cancel();
    }

    private void OnSkipTapped(object sender, TappedEventArgs e)
    {
        _animationCts?.Cancel();
        if (BindingContext is SplashViewModel viewModel)
        {
            viewModel.SkipToNextPage();
        }
    }

    private async Task RunArEvacuationAnimationAsync(CancellationToken ct)
    {
        try
        {
            // Initial element setup
            ProgressBarFill.WidthRequest = 10;
            PeopleGroup.TranslationY = 30;
            Arrow1.Opacity = 0.2;
            Arrow2.Opacity = 0.4;
            Arrow3.Opacity = 0.6;

            // Animate progress bar smoothly over 3 seconds
            _ = ProgressBarFill.LayoutTo(new Rect(ProgressBarFill.X, ProgressBarFill.Y, 200, ProgressBarFill.Height), 3000, Easing.CubicOut);

            while (!ct.IsCancellationRequested)
            {
                // 1. Ambient Glow & Portal pulse
                _ = AmbientGlow.ScaleTo(1.2, 800, Easing.SinInOut);
                _ = ExitPortalBorder.ScaleTo(1.05, 800, Easing.SinInOut);

                // 2. Wave of AR Arrows pointing upward along path
                await Arrow3.FadeTo(1.0, 200);
                _ = Arrow3.TranslateTo(0, -10, 300, Easing.CubicOut);

                await Arrow2.FadeTo(1.0, 200);
                _ = Arrow2.TranslateTo(0, -10, 300, Easing.CubicOut);

                await Arrow1.FadeTo(1.0, 200);
                _ = Arrow1.TranslateTo(0, -10, 300, Easing.CubicOut);

                // 3. People avatars walk forward along the AR evacuation path
                _ = PeopleGroup.TranslateTo(0, -35, 1200, Easing.CubicInOut);
                await Task.Delay(400, ct);

                // Reset arrows & pulse back
                _ = AmbientGlow.ScaleTo(1.0, 800, Easing.SinInOut);
                _ = ExitPortalBorder.ScaleTo(1.0, 800, Easing.SinInOut);

                _ = Arrow3.TranslateTo(0, 0, 300, Easing.CubicIn);
                _ = Arrow3.FadeTo(0.4, 300);

                _ = Arrow2.TranslateTo(0, 0, 300, Easing.CubicIn);
                _ = Arrow2.FadeTo(0.4, 300);

                _ = Arrow1.TranslateTo(0, 0, 300, Easing.CubicIn);
                _ = Arrow1.FadeTo(0.4, 300);

                // People avatar step bounce
                _ = PeopleGroup.TranslateTo(0, 10, 1000, Easing.CubicInOut);
                await Task.Delay(700, ct);
            }
        }
        catch (TaskCanceledException)
        {
            // Animation finished / cancelled gracefully
        }
        catch (Exception)
        {
            // Ignore UI transition errors
        }
    }
}

