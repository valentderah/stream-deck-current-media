using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using CurrentMedia.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-info")]
public class NowPlayingAction : KeypadBase
{
    private class PluginSettings
    {
        public static PluginSettings CreateDefaultSettings()
        {
            return new PluginSettings();
        }

        [JsonProperty(PropertyName = "textDisplayMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public TextDisplayMode TextDisplayMode { get; set; } = TextDisplayMode.Both;

        [JsonProperty(PropertyName = "marqueeSpeed")]
        public int MarqueeSpeed { get; set; } = 40;

        [JsonProperty(PropertyName = "position")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ImagePosition Position { get; set; } = ImagePosition.None;

        [JsonProperty(PropertyName = "action")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ActionType Action { get; set; } = ActionType.Toggle;

        [JsonProperty(PropertyName = "overlayDisplayMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public OverlayDisplayMode OverlayDisplayMode { get; set; } = OverlayDisplayMode.None;

        [JsonProperty(PropertyName = "cropMode")]
        [JsonConverter(typeof(StringEnumConverter))]
        public CropMode CropMode { get; set; } = CropMode.Square;
    }

    private const int ImageSizeFull = 144;
    private const int ImageSizeSingleCell = 72;
    private const string PlaceholderColor = "#000000";

    private readonly PluginSettings _settings;
    private MediaInfo? _currentMediaInfo;
    private int _marqueeOffset;
    private Timer? _marqueeTimer;
    private const double MaxIntervalMs = 1500.0;
    private const double MinIntervalMs = 500.0;
    private const int MarqueeVisibleChars = 10;

    private double GetMarqueeIntervalMs()
    {
        if (_settings.MarqueeSpeed <= 0) return 0;
        var speed = Math.Clamp(_settings.MarqueeSpeed, 1, 100);
        return MaxIntervalMs * Math.Pow(MinIntervalMs / MaxIntervalMs, speed / 100.0);
    }

    private void RestartMarqueeTimer()
    {
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;

        if (_settings.MarqueeSpeed <= 0) return;

        var interval = (int)Math.Round(GetMarqueeIntervalMs());
        _marqueeTimer = new Timer(_ =>
        {
            _marqueeOffset++;
            if (_currentMediaInfo != null)
                _ = UpdateTitleAsync(_currentMediaInfo);
        }, null, interval, interval);
    }

    public NowPlayingAction(ISDConnection connection, InitialPayload payload) : base(connection, payload)
    {
        if (payload.Settings == null || payload.Settings.Count == 0)
        {
            _settings = PluginSettings.CreateDefaultSettings();
        }
        else
        {
            _settings = payload.Settings.ToObject<PluginSettings>() ?? PluginSettings.CreateDefaultSettings();
        }

        MediaSessionManager.Instance.MediaInfoChanged += OnMediaInfoChanged;
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        RestartMarqueeTimer();
        _ = InitializeAndUpdateAsync();
    }

    private async Task InitializeAndUpdateAsync()
    {
        await MediaSessionManager.Instance.InitializeAsync();
        await MediaSessionManager.Instance.RequestUpdateAsync();
    }

    private async void OnPropertyInspectorDidAppear(object? sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
    {
        // Refresh data when Property Inspector opens
        await MediaSessionManager.Instance.RequestUpdateAsync();
    }

    public override void Dispose()
    {
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;
        MediaSessionManager.Instance.MediaInfoChanged -= OnMediaInfoChanged;
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Logger.Instance.LogMessage(TracingLevel.INFO, "NowPlayingAction disposed");
    }

    private async void OnMediaInfoChanged(object? sender, MediaInfo info)
    {
        try
        {
            _currentMediaInfo = info;
            await UpdateDisplayAsync(info);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error handling media info change: {ex.Message}");
        }
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            switch (_settings.Action)
            {
                case ActionType.Toggle:
                    await MediaSessionManager.Instance.TogglePlayPauseAsync();
                    break;
                case ActionType.Next:
                    await MediaSessionManager.Instance.NextTrackAsync();
                    break;
                case ActionType.Previous:
                    await MediaSessionManager.Instance.PreviousTrackAsync();
                    break;
                case ActionType.Forward:
                    await MediaSessionManager.Instance.SeekForwardAsync();
                    break;
                case ActionType.Backward:
                    await MediaSessionManager.Instance.SeekBackwardAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error on key press: {ex.Message}");
        }
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
        RestartMarqueeTimer();
        if (_currentMediaInfo != null)
        {
            _ = UpdateDisplayAsync(_currentMediaInfo);
        }
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    private async Task UpdateDisplayAsync(MediaInfo info)
    {
        await UpdateImageAsync(info);
        await UpdateTitleAsync(info);
    }

    private async Task UpdateImageAsync(MediaInfo info)
    {
        try
        {
            var imageSize = (_settings.Position == ImagePosition.None || _settings.Position == ImagePosition.NoImage)
                ? ImageSizeFull : ImageSizeSingleCell;
            Image? baseImage = null;

            if (_settings.Position != ImagePosition.NoImage)
            {
                var base64Image = info.GetCoverArt(_settings.Position, _settings.CropMode);

                if (!string.IsNullOrEmpty(base64Image))
                {
                    var imageBytes = Convert.FromBase64String(base64Image);
                    using var ms = new MemoryStream(imageBytes);
                    baseImage = Image.FromStream(ms);
                }
            }

            baseImage ??= OverlayRenderer.CreatePlaceholderImage(imageSize, PlaceholderColor);

            Image? finalImage = null;
            try
            {
                if (_settings.OverlayDisplayMode != OverlayDisplayMode.None)
                {
                    finalImage = OverlayRenderer.ApplyOverlay(baseImage, info, _settings.OverlayDisplayMode);
                }
                else
                {
                    finalImage = baseImage;
                    baseImage = null;
                }

                await Connection.SetImageAsync(finalImage);
            }
            finally
            {
                finalImage?.Dispose();
                baseImage?.Dispose();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating image: {ex.Message}");
        }
    }

    private async Task UpdateTitleAsync(MediaInfo info)
    {
        try
        {
            if (!info.HasMediaData)
            {
                await Connection.SetTitleAsync(string.Empty);
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            var textMode = _settings.TextDisplayMode;

            if (textMode == TextDisplayMode.None)
            {
                await Connection.SetTitleAsync(string.Empty);
                return;
            }

            if ((textMode == TextDisplayMode.Both || textMode == TextDisplayMode.Title) && !string.IsNullOrEmpty(info.Title))
            {
                var title = _settings.MarqueeSpeed > 0 ? GetMarqueeText(info.Title) : info.Title;
                parts.Add(title);
            }

            if (textMode == TextDisplayMode.Both || textMode == TextDisplayMode.Artists)
            {
                var artistText = info.Artists.Count > 0 ? string.Join(", ", info.Artists) : info.Artist;
                if (!string.IsNullOrEmpty(artistText))
                {
                    var artist = _settings.MarqueeSpeed > 0 ? GetMarqueeText(artistText) : artistText;
                    parts.Add(artist);
                }
            }

            var displayText = string.Join("\n", parts);
            await Connection.SetTitleAsync(displayText);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating title: {ex.Message}");
        }
    }

    private string GetMarqueeText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= MarqueeVisibleChars)
        {
            return text;
        }

        var paddedText = text + "   " + text;
        var offset = _marqueeOffset % (text.Length + 3);
        return paddedText.Substring(offset, Math.Min(MarqueeVisibleChars, paddedText.Length - offset));
    }
}
