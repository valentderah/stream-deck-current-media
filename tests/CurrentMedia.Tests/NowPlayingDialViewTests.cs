using CurrentMedia.Actions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace CurrentMedia.Tests;

public class NowPlayingDialViewTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly List<JObject> _sent = new();

    private NowPlayingDialView NewView(NowPlayingSettings? settings = null) =>
        new(payload =>
        {
            _sent.Add(payload);
            return Task.CompletedTask;
        }, settings ?? new NowPlayingSettings { MarqueeSpeed = 0 }, _time);

    private static MediaState Song(string title = "Song") => new()
    {
        IsActive = true,
        Title = title,
        Artist = "Artist",
        Status = "Playing"
    };

    private MediaTimeline Timeline(double position, double duration = 200, bool playing = true) =>
        new(position, duration, playing, _time.GetUtcNow());

    [Fact]
    public async Task ShowAsync_SendsTextAndCover()
    {
        using var view = NewView();

        await view.ShowAsync(Song());

        var payload = Assert.Single(_sent);
        Assert.Equal("Song", (string)payload["title"]!);
        Assert.Equal("Artist", (string)payload["artist"]!);
        Assert.StartsWith("data:image/png;base64,", (string)payload["cover"]!);
        Assert.False((bool)payload["progress"]!["enabled"]!);
    }

    [Fact]
    public async Task RenderAsync_NothingChanged_SendsNothing()
    {
        using var view = NewView();
        await view.ShowAsync(Song());

        await view.RenderAsync();

        Assert.Single(_sent);
    }

    [Fact]
    public async Task OnTimelineAsync_ShowsProgress()
    {
        using var view = NewView();
        await view.ShowAsync(Song());

        await view.OnTimelineAsync(Timeline(30));

        Assert.Equal("0:30", (string)_sent[^1]["elapsed"]!["value"]!);
        Assert.Equal("3:20", (string)_sent[^1]["duration"]!["value"]!);
        Assert.Equal(15, (int)_sent[^1]["progress"]!["value"]!);
        Assert.Equal(200, view.Duration);
    }

    [Fact]
    public async Task RenderAsync_AfterOneSecond_AdvancesTheElapsedTime()
    {
        using var view = NewView();
        await view.ShowAsync(Song());
        await view.OnTimelineAsync(Timeline(30));

        _time.Advance(TimeSpan.FromSeconds(1));
        await view.RenderAsync();

        Assert.Equal("0:31", (string)_sent[^1]["elapsed"]!["value"]!);
    }

    [Fact]
    public async Task ShiftAsync_MovesTheBarAtOnce()
    {
        using var view = NewView();
        await view.ShowAsync(Song());
        await view.OnTimelineAsync(Timeline(30, playing: false));

        await view.ShiftAsync(20);

        Assert.Equal("0:50", (string)_sent[^1]["elapsed"]!["value"]!);
        Assert.Equal(25, (int)_sent[^1]["progress"]!["value"]!);
    }

    [Fact]
    public async Task StaleTimelineAfterShift_DoesNotMoveTheBarBack()
    {
        using var view = NewView();
        await view.ShowAsync(Song());
        await view.OnTimelineAsync(Timeline(30, playing: false));
        await view.ShiftAsync(20);
        var before = _sent.Count;

        _time.Advance(TimeSpan.FromMilliseconds(500));
        await view.OnTimelineAsync(Timeline(30, playing: false));

        Assert.Equal(before, _sent.Count);
    }

    [Fact]
    public async Task JumpAsync_SetsAnAbsolutePosition()
    {
        using var view = NewView();
        await view.ShowAsync(Song());
        await view.OnTimelineAsync(Timeline(30, playing: false));

        await view.JumpAsync(100);

        Assert.Equal("1:40", (string)_sent[^1]["elapsed"]!["value"]!);
    }

    [Fact]
    public async Task TrackChange_ReleasesHold()
    {
        using var view = NewView();
        await view.ShowAsync(Song());
        await view.OnTimelineAsync(Timeline(30, playing: false));
        await view.JumpAsync(150);

        await view.ShowAsync(Song("Next song"));
        await view.OnTimelineAsync(Timeline(2, playing: false));

        Assert.Equal("0:02", (string)_sent[^1]["elapsed"]!["value"]!);
    }

    [Fact]
    public async Task FailedSend_IsRetriedInFullOnTheNextRender()
    {
        var attempts = new List<JObject>();
        using var view = new NowPlayingDialView(payload =>
        {
            attempts.Add(payload);
            return attempts.Count == 1
                ? Task.FromException(new InvalidOperationException("socket"))
                : Task.CompletedTask;
        }, new NowPlayingSettings { MarqueeSpeed = 0 }, _time);

        await view.ShowAsync(Song());
        await view.RenderAsync();

        Assert.Equal(2, attempts.Count);
        Assert.Equal("Song", (string)attempts[1]["title"]!);
        Assert.NotNull(attempts[1]["cover"]);
    }

    [Fact]
    public async Task ApplySettingsAsync_RerendersWithTheNewTextMode()
    {
        var settings = new NowPlayingSettings { MarqueeSpeed = 0 };
        using var view = NewView(settings);
        await view.ShowAsync(Song());

        settings.TextDisplayMode = TextDisplayMode.Title;
        await view.ApplySettingsAsync();

        Assert.Equal("", (string)_sent[^1]["artist"]!);
    }

    [Fact]
    public async Task AfterDispose_NothingIsSent()
    {
        var view = NewView();
        view.Dispose();

        await view.ShowAsync(Song());

        Assert.Empty(_sent);
    }
}
