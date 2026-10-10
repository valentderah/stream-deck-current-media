using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

internal sealed class NowPlayingSettings
{
    public static NowPlayingSettings FromPayload(JObject? settings)
    {
        if (settings == null || settings.Count == 0)
        {
            return new NowPlayingSettings();
        }

        try
        {
            return settings.ToObject<NowPlayingSettings>() ?? new NowPlayingSettings();
        }
        catch (JsonException)
        {
            return new NowPlayingSettings();
        }
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

    [JsonProperty(PropertyName = "keyLongPress")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ActionType KeyLongPress { get; set; } = ActionType.None;

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

    [JsonProperty(PropertyName = "dialRotate")]
    [JsonConverter(typeof(StringEnumConverter))]
    public DialRotateMode DialRotate { get; set; } = DialRotateMode.Seek;

    [JsonProperty(PropertyName = "dialPressRotate")]
    [JsonConverter(typeof(StringEnumConverter))]
    public DialRotateMode DialPressRotate { get; set; } = DialRotateMode.Track;

    [JsonProperty(PropertyName = "dialPress")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ActionType DialPress { get; set; } = ActionType.Toggle;

    [JsonProperty(PropertyName = "touchTap")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ActionType TouchTap { get; set; } = ActionType.Toggle;

    [JsonProperty(PropertyName = "touchLongTap")]
    [JsonConverter(typeof(StringEnumConverter))]
    public ActionType TouchLongTap { get; set; } = ActionType.None;

    [JsonProperty(PropertyName = "dialSeekSeconds")]
    public int DialSeekSeconds { get; set; } = SeekSeconds.DialDefault;

    [JsonProperty(PropertyName = "tapSeek")]
    public bool TapSeek { get; set; } = true;
}
