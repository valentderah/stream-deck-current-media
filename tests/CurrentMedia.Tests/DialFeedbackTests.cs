using Newtonsoft.Json.Linq;
using Xunit;

namespace CurrentMedia.Tests;

public class DialFeedbackTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static MediaState Song(string title = "Song") => new()
    {
        IsActive = true,
        Title = title,
        Artist = "Artist",
        Status = "Playing"
    };

    private static DialFrame Frame(double position = 30, double duration = 200, string title = "Song") =>
        DialFrameBuilder.Build(
            Song(title),
            new MediaTimeline(position, duration, false, T0),
            T0,
            TextDisplayMode.Both,
            0,
            0);

    [Fact]
    public void Build_ActiveTrack_FillsEveryField()
    {
        Assert.Equal(new DialFrame("Song", "Artist", "0:30", "3:20", 15, true), Frame());
    }

    [Fact]
    public void Build_ExtrapolatesWhilePlaying()
    {
        var frame = DialFrameBuilder.Build(
            Song(),
            new MediaTimeline(30, 200, true, T0),
            T0.AddSeconds(70),
            TextDisplayMode.Both,
            0,
            0);

        Assert.Equal("1:40", frame.Elapsed);
        Assert.Equal(50, frame.Progress);
    }

    [Fact]
    public void Build_NoDuration_HidesTheTimeline()
    {
        Assert.Equal(new DialFrame("Song", "Artist", "", "", 0, false), Frame(position: 30, duration: 0));
    }

    [Fact]
    public void Build_Inactive_IsEmpty()
    {
        var frame = DialFrameBuilder.Build(
            new MediaState(),
            new MediaTimeline(30, 200, true, T0),
            T0,
            TextDisplayMode.Both,
            0,
            0);

        Assert.Equal(new DialFrame("", "", "", "", 0, false), frame);
    }

    [Fact]
    public void Build_TextModeTitle_DropsTheArtist()
    {
        var frame = DialFrameBuilder.Build(Song(), MediaTimeline.None, T0, TextDisplayMode.Title, 0, 0);

        Assert.Equal("Song", frame.Title);
        Assert.Equal("", frame.Artist);
    }

    [Fact]
    public void Build_Marquee_WindowsLongTitlesToStripWidth()
    {
        var state = Song("A title that is far too long for the strip");

        var still = DialFrameBuilder.Build(state, MediaTimeline.None, T0, TextDisplayMode.Title, 0, 3);
        var moving = DialFrameBuilder.Build(state, MediaTimeline.None, T0, TextDisplayMode.Title, 40, 2);

        Assert.Equal("A title that is far too long for the strip", still.Title);
        Assert.Equal("title that is fa", moving.Title);
        Assert.Equal(DialFrameBuilder.TitleWindowCells, TextFit.Cells(moving.Title));
    }

    [Fact]
    public void Build_Marquee_LeavesLinesThatFitAlone()
    {
        var state = Song("Lonely");
        state.Artist = "Imagine Dragons";

        var frame = DialFrameBuilder.Build(state, MediaTimeline.None, T0, TextDisplayMode.Both, 40, 7);

        Assert.Equal("Lonely", frame.Title);
        Assert.Equal("Imagine Dragons", frame.Artist);
    }

    [Fact]
    public void Next_FirstFrame_CarriesEveryKey()
    {
        var feedback = new DialFeedback();

        var payload = feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        Assert.NotNull(payload);
        Assert.Equal("Song", (string)payload!["title"]!);
        Assert.Equal("Artist", (string)payload["artist"]!);
        Assert.Equal("data:cover", (string)payload["cover"]!);
        Assert.Equal("data:bg", (string)payload["background"]!);
        Assert.Equal("0:30", (string)payload["elapsed"]!["value"]!);
        Assert.True((bool)payload["elapsed"]!["enabled"]!);
        Assert.Equal("3:20", (string)payload["duration"]!["value"]!);
        Assert.Equal(15, (int)payload["progress"]!["value"]!);
        Assert.True((bool)payload["progress"]!["enabled"]!);
    }

    [Fact]
    public void Next_SameFrame_IsNull()
    {
        var feedback = new DialFeedback();
        feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        Assert.Null(feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:bg"));
    }

    [Fact]
    public void Next_OnlyTheElapsedTimeMoved_SendsOnlyThat()
    {
        var feedback = new DialFeedback();
        feedback.Next(Frame(position: 30, duration: 2000), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        var payload = feedback.Next(Frame(position: 31, duration: 2000), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        Assert.Equal(new[] { "elapsed" }, payload!.Properties().Select(p => p.Name).ToArray());
        Assert.Equal("0:31", (string)payload["elapsed"]!["value"]!);
    }

    [Fact]
    public void Next_UnchangedCoverKey_DoesNotRenderTheCover()
    {
        var feedback = new DialFeedback();
        var renders = 0;
        feedback.Next(Frame(), "cover:1", () => { renders++; return "data:cover"; }, "bg:1", () => "data:bg");

        feedback.Next(Frame(title: "Other"), "cover:1", () => { renders++; return "data:cover"; }, "bg:1", () => "data:bg");

        Assert.Equal(1, renders);
    }

    [Fact]
    public void Next_NewCoverKey_SendsTheCover()
    {
        var feedback = new DialFeedback();
        feedback.Next(Frame(), "cover:1", () => "data:one", "bg:1", () => "data:bg");

        var payload = feedback.Next(Frame(), "cover:2", () => "data:two", "bg:1", () => "data:bg");

        Assert.Equal(new[] { "cover" }, payload!.Properties().Select(p => p.Name).ToArray());
        Assert.Equal("data:two", (string)payload["cover"]!);
    }

    [Fact]
    public void Next_NewBackgroundKey_SendsOnlyTheBackground()
    {
        var feedback = new DialFeedback();
        feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:one");

        var payload = feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:2", () => "data:two");

        Assert.Equal(new[] { "background" }, payload!.Properties().Select(p => p.Name).ToArray());
        Assert.Equal("data:two", (string)payload["background"]!);
    }

    [Fact]
    public void Next_TimelineDisappears_DisablesAllThreeItems()
    {
        var feedback = new DialFeedback();
        feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        var payload = feedback.Next(Frame(duration: 0), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        Assert.False((bool)payload!["elapsed"]!["enabled"]!);
        Assert.False((bool)payload["duration"]!["enabled"]!);
        Assert.False((bool)payload["progress"]!["enabled"]!);
    }

    [Fact]
    public void Reset_MakesTheNextFrameFullAgain()
    {
        var feedback = new DialFeedback();
        feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        feedback.Reset();
        var payload = feedback.Next(Frame(), "cover:1", () => "data:cover", "bg:1", () => "data:bg");

        Assert.Equal(7, payload!.Count);
    }
}
