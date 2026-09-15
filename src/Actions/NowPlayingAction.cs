using System;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using CurrentMedia.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

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

        [JsonProperty(PropertyName = "seconds")]
        public int Seconds { get; set; } = SeekSeconds.Default;

        [JsonProperty(PropertyName = "idleImage")]
        public string IdleImage { get; set; } = "";

        [JsonProperty(PropertyName = "idleImageName")]
        public string IdleImageName { get; set; } = "";
    }

    private readonly PluginSettings _settings;
    private MediaState? _currentMediaState;
    private int _marqueeOffset;
    private Timer? _marqueeTimer;
    private const double MaxIntervalMs = 1500.0;
    private const double MinIntervalMs = 500.0;

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
            if (_currentMediaState != null)
                _ = UpdateTitleAsync(_currentMediaState);
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

        MediaManagerProvider.Instance.MediaStateChanged += OnMediaStateChanged;
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin += OnSendToPlugin;
        RestartMarqueeTimer();
        _ = InitializeAndUpdateAsync();
    }

    private async Task InitializeAndUpdateAsync()
    {
        await MediaManagerProvider.Instance.InitializeAsync();
        await MediaManagerProvider.Instance.RequestUpdateAsync();
    }

    private async void OnPropertyInspectorDidAppear(object? sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
    {
        await MediaManagerProvider.Instance.RequestUpdateAsync();
    }

    public override void Dispose()
    {
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;
        MediaManagerProvider.Instance.MediaStateChanged -= OnMediaStateChanged;
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorDidAppear;
        Connection.OnSendToPlugin -= OnSendToPlugin;
        Logger.Instance.LogMessage(TracingLevel.INFO, "NowPlayingAction disposed");
    }

    private async void OnMediaStateChanged(object? sender, MediaState state)
    {
        try
        {
            _currentMediaState = state;
            await UpdateDisplayAsync(state);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error handling media state change: {ex.Message}");
        }
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        try
        {
            var seekSeconds = SeekSeconds.Normalize(_settings.Seconds);
            switch (_settings.Action)
            {
                case ActionType.Toggle:
                    await MediaManagerProvider.Instance.PlayPauseAsync();
                    break;
                case ActionType.Next:
                    await MediaManagerProvider.Instance.NextAsync();
                    break;
                case ActionType.Previous:
                    await MediaManagerProvider.Instance.PreviousAsync();
                    break;
                case ActionType.Forward:
                    await MediaManagerProvider.Instance.SeekByAsync(seekSeconds);
                    break;
                case ActionType.Backward:
                    await MediaManagerProvider.Instance.SeekByAsync(-seekSeconds);
                    break;
                case ActionType.None:
                    break;
                default:
                {
                    ActionType unreachable = _settings.Action;
                    throw new InvalidOperationException($"Unhandled action: {unreachable}");
                }
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
        _ = UpdateDisplayAsync(_currentMediaState ?? new MediaState());
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    private void OnSendToPlugin(object? sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
    {
        if (e.Event.Payload?["event"]?.ToString() == "pickIdleImage")
        {
            _ = PickIdleImageAsync();
        }
    }

    private async Task PickIdleImageAsync()
    {
        try
        {
            var path = await Task.Run(() => NativeIdleImagePicker.Pick());
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var png = await Task.Run(() => IdleImageHelper.TryLoadFromFile(path));
            if (png == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Could not load idle image from {path}");
                return;
            }

            _settings.IdleImage = png;
            _settings.IdleImageName = Path.GetFileName(path);
            await Connection.SetSettingsAsync(JObject.FromObject(_settings));
            await UpdateDisplayAsync(_currentMediaState ?? new MediaState());
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error picking idle image: {ex.Message}");
        }
    }

    private async Task UpdateDisplayAsync(MediaState state)
    {
        await UpdateImageAsync(state);
        await UpdateTitleAsync(state);
    }

    private async Task UpdateImageAsync(MediaState state)
    {
        try
        {
            var hasMedia = state.HasMediaData;
            if (!hasMedia)
            {
                if (!string.IsNullOrEmpty(_settings.IdleImage))
                {
                    await Connection.SetImageAsync(_settings.IdleImage);
                    return;
                }

                var imageSize = (_settings.Position is ImagePosition.None or ImagePosition.NoImage)
                    ? ImagePipeline.TargetSize
                    : ImagePipeline.PartSize;
                using var transparent = ImageExtensions.CreateTransparent(imageSize);
                await Connection.SetImageAsync(ImageExtensions.ToPngDataUri(transparent));
                return;
            }

            var dataUri = ImagePipeline.RenderForPosition(
                state,
                _settings.Position,
                _settings.CropMode,
                _settings.OverlayDisplayMode,
                _settings.IdleImage);
            await Connection.SetImageAsync(dataUri);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating image: {ex.Message}");
        }
    }

    private async Task UpdateTitleAsync(MediaState state)
    {
        try
        {
            if (!state.IsActive || !state.HasMediaData)
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

            if ((textMode == TextDisplayMode.Both || textMode == TextDisplayMode.Title) && !string.IsNullOrEmpty(state.Title))
            {
                var title = _settings.MarqueeSpeed > 0
                    ? MarqueeHelper.GetMarqueeText(state.Title, _marqueeOffset)
                    : state.Title;
                parts.Add(title);
            }

            if (textMode == TextDisplayMode.Both || textMode == TextDisplayMode.Artists)
            {
                var artistText = state.Artists.Count > 0 ? string.Join(", ", state.Artists) : state.Artist;
                if (!string.IsNullOrEmpty(artistText))
                {
                    var artist = _settings.MarqueeSpeed > 0
                        ? MarqueeHelper.GetMarqueeText(artistText, _marqueeOffset)
                        : artistText;
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
}
