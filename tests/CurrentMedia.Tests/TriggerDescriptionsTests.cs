using CurrentMedia.Actions;
using Xunit;

namespace CurrentMedia.Tests;

public class TriggerDescriptionsTests
{
    private static readonly PluginStrings Strings = PluginStrings.Load(RepoPaths.Assets, "en");

    [Fact]
    public void Build_Defaults_DescribeTheDefaultGestures()
    {
        var descriptions = TriggerDescriptions.Build(new NowPlayingSettings(), Strings);

        Assert.Equal("Seek in Track", (string)descriptions["rotate"]!);
        Assert.Equal("Play/Pause", (string)descriptions["push"]!);
        Assert.Equal("Play/Pause", (string)descriptions["touch"]!);
        Assert.Equal("", (string)descriptions["longTouch"]!);
    }

    [Fact]
    public void Build_Remapped_FollowsTheSettings()
    {
        var settings = new NowPlayingSettings
        {
            DialRotate = DialRotateMode.Track,
            DialPress = ActionType.Next,
            TouchTap = ActionType.None,
            TouchLongTap = ActionType.Backward
        };

        var descriptions = TriggerDescriptions.Build(settings, Strings);

        Assert.Equal("Switch Track", (string)descriptions["rotate"]!);
        Assert.Equal("Next Track", (string)descriptions["push"]!);
        Assert.Equal("", (string)descriptions["touch"]!);
        Assert.Equal("Seek Backward", (string)descriptions["longTouch"]!);
    }

    [Fact]
    public void Build_RotateNone_IsEmpty()
    {
        var settings = new NowPlayingSettings { DialRotate = DialRotateMode.None };

        Assert.Equal("", (string)TriggerDescriptions.Build(settings, Strings)["rotate"]!);
    }

    [Fact]
    public void RawSend_IsStillPresentInTheLibrary()
    {
        Assert.True(TriggerDescriptions.IsAvailable);
    }
}
