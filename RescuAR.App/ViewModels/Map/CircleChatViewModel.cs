using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using RescuAR.App.Models;
using RescuAR.App.Services.Cloud;

namespace RescuAR.App.ViewModels.Map;

public class ChatMessageItem
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderAvatarUrl { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "Text";
    public bool HasMedia => !string.IsNullOrWhiteSpace(MediaUrl);
    public bool IsImage => HasMedia && MediaType.Equals("Image", StringComparison.OrdinalIgnoreCase);
    public bool IsVideo => HasMedia && MediaType.Equals("Video", StringComparison.OrdinalIgnoreCase);
    public bool HasText => !string.IsNullOrWhiteSpace(MessageText);
    public bool IsMyMessage { get; set; }
    public bool IsNotMyMessage => !IsMyMessage;
    public DateTime CreatedAt { get; set; }
    public string FormattedTime => CreatedAt.ToLocalTime().ToString("h:mm tt");
}

[QueryProperty(nameof(CircleId), "circleId")]
[QueryProperty(nameof(CircleName), "circleName")]
public partial class CircleChatViewModel : ObservableObject
{
    private readonly SafetyCircleService _safetyCircleService;
    private IDispatcherTimer? _chatTimer;
    private string _currentUserId = string.Empty;

    [ObservableProperty]
    private string circleId = string.Empty;

    [ObservableProperty]
    private string circleName = "Family Circle Chat";

    [ObservableProperty]
    private string newMessageText = string.Empty;

    [ObservableProperty]
    private bool isUploading = false;

    [ObservableProperty]
    private string uploadStatusText = string.Empty;

    public ObservableCollection<ChatMessageItem> Messages { get; } = new();

    public CircleChatViewModel(SafetyCircleService safetyCircleService)
    {
        _safetyCircleService = safetyCircleService;

        try
        {
            _currentUserId = _safetyCircleService.GetCurrentUserId();
        }
        catch { }

        _chatTimer = Application.Current?.Dispatcher?.CreateTimer();
        if (_chatTimer != null)
        {
            _chatTimer.Interval = TimeSpan.FromSeconds(3);
            _chatTimer.Tick += async (s, e) => await RefreshMessagesSilentAsync();
        }
    }

    partial void OnCircleIdChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadMessagesAsync();
            _chatTimer?.Start();
        }
    }

    public async Task InitializeWithCircleAsync(string id, string name)
    {
        CircleId = id;
        if (!string.IsNullOrWhiteSpace(name))
        {
            CircleName = name;
        }

        await LoadMessagesAsync();
        _chatTimer?.Start();
    }

    public void StopTimer()
    {
        _chatTimer?.Stop();
    }

    public async Task LoadMessagesAsync()
    {
        if (string.IsNullOrEmpty(CircleId)) return;

        try
        {
            var rawMsgs = await _safetyCircleService.GetCircleMessagesAsync(CircleId);
            UpdateMessagesCollection(rawMsgs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadMessages error: {ex.Message}");
        }
    }

    private async Task RefreshMessagesSilentAsync()
    {
        if (string.IsNullOrEmpty(CircleId)) return;

        try
        {
            var rawMsgs = await _safetyCircleService.GetCircleMessagesAsync(CircleId);
            if (rawMsgs.Count != Messages.Count)
            {
                UpdateMessagesCollection(rawMsgs);
            }
        }
        catch { }
    }

    private void UpdateMessagesCollection(System.Collections.Generic.List<SupabaseCircleMessage> rawMsgs)
    {
        Messages.Clear();
        foreach (var m in rawMsgs)
        {
            Messages.Add(new ChatMessageItem
            {
                Id = m.Id,
                UserId = m.UserId,
                SenderName = m.SenderName,
                SenderAvatarUrl = m.SenderAvatarUrl,
                MessageText = m.MessageText,
                MediaUrl = m.MediaUrl,
                MediaType = m.MediaType,
                IsMyMessage = m.UserId == _currentUserId,
                CreatedAt = m.CreatedAt
            });
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessageText) || string.IsNullOrEmpty(CircleId)) return;

        var textToSend = NewMessageText.Trim();
        NewMessageText = string.Empty;

        var sentMsg = await _safetyCircleService.SendMessageAsync(CircleId, textToSend, null, "Text");
        if (sentMsg != null)
        {
            Messages.Add(new ChatMessageItem
            {
                Id = sentMsg.Id,
                UserId = sentMsg.UserId,
                SenderName = sentMsg.SenderName,
                SenderAvatarUrl = sentMsg.SenderAvatarUrl,
                MessageText = sentMsg.MessageText,
                MediaUrl = sentMsg.MediaUrl,
                MediaType = sentMsg.MediaType,
                IsMyMessage = true,
                CreatedAt = sentMsg.CreatedAt
            });
        }
    }

    [RelayCommand]
    private async Task CapturePhotoAsync()
    {
        if (string.IsNullOrEmpty(CircleId)) return;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Permission Denied", "Camera permission is required to capture status photos.", "OK");
                return;
            }

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Unavailable", "Camera capture is not supported on this device.", "OK");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                await UploadAndSendMediaAsync(photo, "Image");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CapturePhoto error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PickMediaAsync()
    {
        if (string.IsNullOrEmpty(CircleId)) return;

        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync();
            if (file != null)
            {
                await UploadAndSendMediaAsync(file, "Image");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PickMedia error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PickVideoAsync()
    {
        if (string.IsNullOrEmpty(CircleId)) return;

        try
        {
            var file = await MediaPicker.Default.PickVideoAsync();
            if (file != null)
            {
                await UploadAndSendMediaAsync(file, "Video");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PickVideo error: {ex.Message}");
        }
    }

    private async Task UploadAndSendMediaAsync(FileResult file, string mediaType)
    {
        IsUploading = true;
        UploadStatusText = $"Uploading {mediaType.ToLower()}...";

        try
        {
            using var stream = await file.OpenReadAsync();
            var uploadedUrl = await CloudinaryService.UploadImageStreamAsync(stream, file.FileName);

            if (!string.IsNullOrWhiteSpace(uploadedUrl))
            {
                var caption = !string.IsNullOrWhiteSpace(NewMessageText) ? NewMessageText.Trim() : string.Empty;
                NewMessageText = string.Empty;

                var sentMsg = await _safetyCircleService.SendMessageAsync(CircleId, caption, uploadedUrl, mediaType);
                if (sentMsg != null)
                {
                    Messages.Add(new ChatMessageItem
                    {
                        Id = sentMsg.Id,
                        UserId = sentMsg.UserId,
                        SenderName = sentMsg.SenderName,
                        SenderAvatarUrl = sentMsg.SenderAvatarUrl,
                        MessageText = sentMsg.MessageText,
                        MediaUrl = sentMsg.MediaUrl,
                        MediaType = sentMsg.MediaType,
                        IsMyMessage = true,
                        CreatedAt = sentMsg.CreatedAt
                    });
                }
            }
            else
            {
                if (Shell.Current != null)
                    await Shell.Current.DisplayAlert("Upload Failed", "Could not upload the media. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UploadAndSendMedia error: {ex.Message}");
        }
        finally
        {
            IsUploading = false;
            UploadStatusText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        StopTimer();
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
