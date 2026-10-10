using CurrentMedia.Actions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CurrentMedia.Tests;

public class NowPlayingSettingsTests
{
    [Fact]
    public void FromPayload_Null_ReturnsDefaults()
    {
        var settings = NowPlayingSettings.FromPayload(null);

        Assert.Equal(TextDisplayMode.Both, settings.TextDisplayMode);
        Assert.Equal(40, settings.MarqueeSpeed);
        Assert.Equal(ImagePosition.None, settings.Position);
        Assert.Equal(ActionType.Toggle, settings.Action);
        Assert.Equal(OverlayDisplayMode.None, settings.OverlayDisplayMode);
        Assert.Equal(CropMode.Square, settings.CropMode);
        Assert.Equal(SeekSeconds.Default, settings.Seconds);
        Assert.Equal("", settings.IdleImage);
        Assert.Equal("", settings.IdleImageName);
    }

    [Fact]
    public void FromPayload_SavedByVersion213_KeepsEveryValue()
    {
        var saved = JObject.Parse("""
            {
              "textDisplayMode": "artists",
              "marqueeSpeed": 75,
              "position": "top-right",
              "action": "backward",
              "overlayDisplayMode": "icon",
              "cropMode": "fit",
              "seconds": 30,
              "idleImage": "data:image/png;base64,AAAA",
              "idleImageName": "bg.png"
            }
            """);

        var settings = NowPlayingSettings.FromPayload(saved);

        Assert.Equal(TextDisplayMode.Artists, settings.TextDisplayMode);
        Assert.Equal(75, settings.MarqueeSpeed);
        Assert.Equal(ImagePosition.TopRight, settings.Position);
        Assert.Equal(ActionType.Backward, settings.Action);
        Assert.Equal(ActionType.None, settings.KeyLongPress);
        Assert.Equal(OverlayDisplayMode.Icon, settings.OverlayDisplayMode);
        Assert.Equal(CropMode.Fit, settings.CropMode);
        Assert.Equal(30, settings.Seconds);
        Assert.Equal("data:image/png;base64,AAAA", settings.IdleImage);
        Assert.Equal("bg.png", settings.IdleImageName);
    }

    [Fact]
    public void FromPayload_UnknownEnumValue_ReturnsDefaultsInsteadOfThrowing()
    {
        var saved = JObject.Parse("""{ "action": "volume-up", "marqueeSpeed": 75 }""");

        var settings = NowPlayingSettings.FromPayload(saved);

        Assert.Equal(ActionType.Toggle, settings.Action);
    }

    [Fact]
    public void FromPayload_KeyLongPress_IsRead()
    {
        var saved = JObject.Parse("""{ "action": "toggle", "keyLongPress": "next" }""");

        Assert.Equal(ActionType.Next, NowPlayingSettings.FromPayload(saved).KeyLongPress);
    }

    [Fact]
    public void Serialized_UsesTheSameKeys()
    {
        var json = JObject.FromObject(new NowPlayingSettings());

        foreach (var key in new[]
                 {
                     "textDisplayMode", "marqueeSpeed", "position", "action", "overlayDisplayMode",
                     "cropMode", "seconds", "idleImage", "idleImageName"
                 })
        {
            Assert.True(json.ContainsKey(key), $"missing key {key}");
        }
    }

    [Fact]
    public void FromPayload_SavedByVersion213_GetsDialDefaults()
    {
        var saved = JObject.Parse("""{ "action": "next", "seconds": 30 }""");

        var settings = NowPlayingSettings.FromPayload(saved);

        Assert.Equal(DialRotateMode.Seek, settings.DialRotate);
        Assert.Equal(DialRotateMode.Track, settings.DialPressRotate);
        Assert.Equal(ActionType.Toggle, settings.DialPress);
        Assert.Equal(ActionType.Toggle, settings.TouchTap);
        Assert.Equal(ActionType.None, settings.TouchLongTap);
        Assert.Equal(5, settings.DialSeekSeconds);
        Assert.True(settings.TapSeek);
    }

    [Fact]
    public void FromPayload_DialSettings_AreRead()
    {
        var saved = JObject.Parse("""
            {
              "dialRotate": "track",
              "dialPressRotate": "none",
              "dialPress": "next",
              "touchTap": "previous",
              "touchLongTap": "toggle",
              "dialSeekSeconds": 15,
              "tapSeek": false
            }
            """);

        var settings = NowPlayingSettings.FromPayload(saved);

        Assert.Equal(DialRotateMode.Track, settings.DialRotate);
        Assert.Equal(DialRotateMode.None, settings.DialPressRotate);
        Assert.Equal(ActionType.Next, settings.DialPress);
        Assert.Equal(ActionType.Previous, settings.TouchTap);
        Assert.Equal(ActionType.Toggle, settings.TouchLongTap);
        Assert.Equal(15, settings.DialSeekSeconds);
        Assert.False(settings.TapSeek);
    }

    [Fact]
    public void FromPayload_UnknownRotateMode_ReturnsDefaultsInsteadOfThrowing()
    {
        var saved = JObject.Parse("""{ "dialRotate": "volume" }""");

        Assert.Equal(DialRotateMode.Seek, NowPlayingSettings.FromPayload(saved).DialRotate);
    }
}
