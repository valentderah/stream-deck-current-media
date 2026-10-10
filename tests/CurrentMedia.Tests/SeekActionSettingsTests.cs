using CurrentMedia.Actions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CurrentMedia.Tests;

public class SeekActionSettingsTests
{
    [Fact]
    public void FromPayload_NullOrEmpty_ReturnsDefaults()
    {
        Assert.Equal(SeekSeconds.Default, SeekActionSettings.FromPayload(null).Seconds);
        Assert.Equal(SeekSeconds.Default, SeekActionSettings.FromPayload(new JObject()).Seconds);
    }

    [Fact]
    public void FromPayload_ValidSeconds_ReadsValue()
    {
        var settings = SeekActionSettings.FromPayload(new JObject { ["seconds"] = 15 });

        Assert.Equal(15, settings.Seconds);
    }

    [Fact]
    public void FromPayload_NonIntegerSeconds_ReturnsDefaults()
    {
        var settings = SeekActionSettings.FromPayload(new JObject { ["seconds"] = "abc" });

        Assert.Equal(SeekSeconds.Default, settings.Seconds);
    }
}
