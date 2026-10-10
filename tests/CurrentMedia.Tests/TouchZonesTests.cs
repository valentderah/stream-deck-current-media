using Newtonsoft.Json.Linq;
using Xunit;

namespace CurrentMedia.Tests;

public class TouchZonesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(65, false)]
    [InlineData(66, true)]
    [InlineData(99, true)]
    public void IsProgressZone_IsTheLowerThird(int y, bool expected)
    {
        Assert.Equal(expected, TouchZones.IsProgressZone(y));
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(54, 0.0)]
    [InlineData(100, 0.5)]
    [InlineData(146, 1.0)]
    [InlineData(199, 1.0)]
    public void Fraction_MapsXAcrossTheBarAndClamps(int x, double expected)
    {
        Assert.Equal(expected, TouchZones.Fraction(x), 3);
    }

    [Fact]
    public void SeekTarget_TapOnTheBar_ReturnsThePosition()
    {
        Assert.Equal(120.0, TouchZones.SeekTarget(new[] { 100, 85 }, true, 240)!.Value, 3);
    }

    [Fact]
    public void SeekTarget_TapAboveTheBar_IsNull()
    {
        Assert.Null(TouchZones.SeekTarget(new[] { 100, 30 }, true, 240));
    }

    [Theory]
    [InlineData(54, 0.0)]
    [InlineData(146, 240.0)]
    public void SeekTarget_TapOnTheBarEdges_ReturnsTheEnds(int x, double expected)
    {
        Assert.Equal(expected, TouchZones.SeekTarget(new[] { x, 85 }, true, 240)!.Value, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    [InlineData(147)]
    [InlineData(199)]
    public void SeekTarget_TapOnATimeLabel_IsNull(int x)
    {
        Assert.Null(TouchZones.SeekTarget(new[] { x, 85 }, true, 240));
    }

    [Fact]
    public void SeekTarget_TapSeekDisabled_IsNull()
    {
        Assert.Null(TouchZones.SeekTarget(new[] { 100, 85 }, false, 240));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SeekTarget_NoUsableDuration_IsNull(double duration)
    {
        Assert.Null(TouchZones.SeekTarget(new[] { 100, 85 }, true, duration));
    }

    [Fact]
    public void SeekTarget_MissingOrShortTapPosition_IsNull()
    {
        Assert.Null(TouchZones.SeekTarget(null, true, 240));
        Assert.Null(TouchZones.SeekTarget(Array.Empty<int>(), true, 240));
        Assert.Null(TouchZones.SeekTarget(new[] { 100 }, true, 240));
    }

    [Fact]
    public void Layout_DeclaresEveryItemAndMatchesTheConstants()
    {
        var layout = JObject.Parse(File.ReadAllText(Path.Combine(RepoPaths.Assets, "layouts", "now-playing.json")));
        var items = ((JArray)layout["items"]!).ToDictionary(item => (string)item["key"]!, item => item);

        Assert.Equal(
            new[] { "artist", "cover", "duration", "elapsed", "progress", "title" },
            items.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());

        var bar = items["progress"]["rect"]!.Values<int>().ToArray();
        Assert.Equal(TouchZones.BarLeft, bar[0]);
        Assert.Equal(TouchZones.BarWidth, bar[2]);
        Assert.True(bar[1] >= TouchZones.ProgressZoneTop, "the bar must sit inside the progress zone");

        var cover = items["cover"]["rect"]!.Values<int>().ToArray();
        Assert.Equal(TouchZones.CoverSize, cover[2]);
        Assert.Equal(TouchZones.CoverSize, cover[3]);

        Assert.Equal(TouchZones.TextWidth, items["title"]["rect"]!.Values<int>().ElementAt(2));
        Assert.Equal(TouchZones.TextWidth, items["artist"]["rect"]!.Values<int>().ElementAt(2));
        Assert.Equal(TouchZones.TitleFontSize, (int)items["title"]["font"]!["size"]!);
        Assert.Equal(TouchZones.ArtistFontSize, (int)items["artist"]["font"]!["size"]!);

        foreach (var item in items.Values)
        {
            var rect = item["rect"]!.Values<int>().ToArray();
            Assert.True(rect[0] >= 0 && rect[0] + rect[2] <= 200, $"{item["key"]} leaves the segment horizontally");
            Assert.True(rect[1] >= 0 && rect[1] + rect[3] <= 100, $"{item["key"]} leaves the segment vertically");
        }
    }
}
