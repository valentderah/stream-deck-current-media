using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

[PluginActionId("ru.valentderah.current-media.media-info")]
public class NowPlayingAction : KeypadBase
{
    private class PluginSettings
    {
        public static PluginSettings CreateDefaultSettings()
        {
            return new PluginSettings
            {
                TextDisplayMode = "both",
                EnableMarquee = true,
                Position = "none",
                Action = "toggle",
                OverlayDisplayMode = "none"
            };
        }

        [JsonProperty(PropertyName = "textDisplayMode")]
        public string TextDisplayMode { get; set; } = "both";

        [JsonProperty(PropertyName = "enableMarquee")]
        public bool EnableMarquee { get; set; } = true;

        [JsonProperty(PropertyName = "position")]
        public string Position { get; set; } = "none";

        [JsonProperty(PropertyName = "action")]
        public string Action { get; set; } = "toggle";

        [JsonProperty(PropertyName = "overlayDisplayMode")]
        public string OverlayDisplayMode { get; set; } = "none";
    }

    private const int ImageSizeFull = 144;
    private const int ImageSizeSingleCell = 72;
    private const string PlaceholderColor = "#1F1F1F";

    private readonly PluginSettings _settings;
    private MediaInfo? _currentMediaInfo;
    private int _marqueeOffset;
    private DateTime _lastMarqueeUpdate = DateTime.MinValue;
    private const int MarqueeUpdateIntervalMs = 300;
    private const int MarqueeVisibleChars = 10;

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
        _ = InitializeAndUpdateAsync();
    }

    private async Task InitializeAndUpdateAsync()
    {
        await MediaSessionManager.Instance.InitializeAsync();
        // Request immediate update when action is created
        await MediaSessionManager.Instance.RequestUpdateAsync();
    }

    private async void OnPropertyInspectorDidAppear(object? sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
    {
        // Refresh data when Property Inspector opens
        await MediaSessionManager.Instance.RequestUpdateAsync();
    }

    public override void Dispose()
    {
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
                case "toggle":
                    await MediaSessionManager.Instance.TogglePlayPauseAsync();
                    break;
                case "next":
                    await MediaSessionManager.Instance.NextTrackAsync();
                    break;
                case "previous":
                    await MediaSessionManager.Instance.PreviousTrackAsync();
                    break;
                case "forward":
                    await MediaSessionManager.Instance.SeekForwardAsync();
                    break;
                case "backward":
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

    public override async void OnTick()
    {
        if (_currentMediaInfo != null && _settings.EnableMarquee)
        {
            var now = DateTime.Now;
            if ((now - _lastMarqueeUpdate).TotalMilliseconds >= MarqueeUpdateIntervalMs)
            {
                _marqueeOffset++;
                _lastMarqueeUpdate = now;
                await UpdateTitleAsync(_currentMediaInfo);
            }
        }
    }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
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
            var imageSize = _settings.Position == "none" ? ImageSizeFull : ImageSizeSingleCell;
            string? base64Image = null;

            if (_settings.Position == "none")
            {
                base64Image = info.CoverArtBase64;
            }
            else
            {
                base64Image = _settings.Position switch
                {
                    "top-left" => info.CoverArtPart1Base64,
                    "top-right" => info.CoverArtPart2Base64,
                    "bottom-left" => info.CoverArtPart3Base64,
                    "bottom-right" => info.CoverArtPart4Base64,
                    _ => info.CoverArtBase64
                };
            }

            Image? baseImage = null;

            if (!string.IsNullOrEmpty(base64Image))
            {
                var imageBytes = Convert.FromBase64String(base64Image);
                using var ms = new MemoryStream(imageBytes);
                baseImage = Image.FromStream(ms);
            }
            else
            {
                baseImage = CreatePlaceholderImage(imageSize);
            }

            if (baseImage != null)
            {
                Image? finalImage = null;
                try
                {
                    if (_settings.OverlayDisplayMode != "none")
                    {
                        finalImage = ApplyOverlay(baseImage, info, _settings.OverlayDisplayMode);
                    }
                    else
                    {
                        finalImage = baseImage;
                        baseImage = null; // Prevent double dispose
                    }

                    await Connection.SetImageAsync(finalImage);
                }
                finally
                {
                    finalImage?.Dispose();
                    baseImage?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating image: {ex.Message}");
        }
    }

    private Image CreatePlaceholderImage(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(ColorTranslator.FromHtml(PlaceholderColor));
        g.FillRectangle(brush, 0, 0, size, size);
        return bitmap;
    }

    private Image ApplyOverlay(Image baseImage, MediaInfo info, string overlayMode)
    {
        var result = new Bitmap(baseImage.Width, baseImage.Height);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);

        var padding = (int)(baseImage.Width * 0.05);
        var iconSize = (int)(baseImage.Width * 0.25);

        var showIcon = (overlayMode == "icon" || overlayMode == "both") && !string.IsNullOrEmpty(info.AppIconBase64);
        var showStatus = (overlayMode == "status" || overlayMode == "both") && !string.IsNullOrEmpty(info.Status);

        if (showIcon && !string.IsNullOrEmpty(info.AppIconBase64))
        {
            try
            {
                var iconBytes = Convert.FromBase64String(info.AppIconBase64);
                using var iconMs = new MemoryStream(iconBytes);
                using var iconImage = Image.FromStream(iconMs);

                var iconX = padding;
                var iconY = padding;
                var radius = iconSize / 2;

                using var bgBrush = new SolidBrush(Color.FromArgb(153, 0, 0, 0));
                g.FillEllipse(bgBrush, iconX - 2, iconY - 2, iconSize + 4, iconSize + 4);

                g.DrawImage(iconImage, iconX, iconY, iconSize, iconSize);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error drawing app icon overlay: {ex.Message}");
            }
        }

        if (showStatus)
        {
            var statusX = baseImage.Width - padding - iconSize;
            var statusY = padding;
            var radius = iconSize / 2;

            using var bgBrush = new SolidBrush(Color.FromArgb(153, 0, 0, 0));
            g.FillEllipse(bgBrush, statusX - 2, statusY - 2, iconSize + 4, iconSize + 4);

            using var iconBrush = new SolidBrush(Color.White);
            var centerX = statusX + radius;
            var centerY = statusY + radius;
            var symbolSize = iconSize * 0.5f;

            if (info.Status == "Playing")
            {
                var points = new PointF[]
                {
                    new PointF(centerX - symbolSize / 3, centerY - symbolSize / 2),
                    new PointF(centerX + symbolSize / 2, centerY),
                    new PointF(centerX - symbolSize / 3, centerY + symbolSize / 2)
                };
                g.FillPolygon(iconBrush, points);
            }
            else
            {
                var barWidth = symbolSize / 4;
                var barHeight = symbolSize;
                g.FillRectangle(iconBrush, centerX - symbolSize / 3, centerY - barHeight / 2, barWidth, barHeight);
                g.FillRectangle(iconBrush, centerX + symbolSize / 6 - barWidth / 2, centerY - barHeight / 2, barWidth, barHeight);
            }
        }

        return result;
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

            if (textMode == "none")
            {
                await Connection.SetTitleAsync(string.Empty);
                return;
            }

            if ((textMode == "both" || textMode == "title") && !string.IsNullOrEmpty(info.Title))
            {
                var title = _settings.EnableMarquee ? GetMarqueeText(info.Title) : info.Title;
                parts.Add(title);
            }

            if ((textMode == "both" || textMode == "artists"))
            {
                var artistText = info.Artists.Count > 0 ? string.Join(", ", info.Artists) : info.Artist;
                if (!string.IsNullOrEmpty(artistText))
                {
                    var artist = _settings.EnableMarquee ? GetMarqueeText(artistText) : artistText;
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
