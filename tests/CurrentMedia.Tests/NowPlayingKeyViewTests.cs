using CurrentMedia.Actions;
using CurrentMedia.Imaging;
using Xunit;

namespace CurrentMedia.Tests;

public class NowPlayingKeyViewTests
{
    [Fact]
    public async Task ShowAsync_InactiveState_SendsImageAndEmptyTitle()
    {
        var images = new List<string>();
        var titles = new List<string>();
        using var view = new NowPlayingKeyView(
            image => { images.Add(image); return Task.CompletedTask; },
            title => { titles.Add(title); return Task.CompletedTask; },
            new NowPlayingSettings());

        await view.ShowAsync(new MediaState());

        Assert.Single(images);
        Assert.StartsWith("data:image/png;base64,", images[0]);
        Assert.Equal(new[] { "" }, titles);
    }

    [Fact]
    public async Task ShowAsync_InactiveStateWithIdleImage_SendsTheIdleImage()
    {
        var images = new List<string>();
        var settings = new NowPlayingSettings { IdleImage = "data:image/png;base64,AAAA" };
        using var view = new NowPlayingKeyView(
            image => { images.Add(image); return Task.CompletedTask; },
            _ => Task.CompletedTask,
            settings);

        await view.ShowAsync(new MediaState());

        Assert.Equal(new[] { "data:image/png;base64,AAAA" }, images);
    }

    [Fact]
    public async Task ShowAsync_ActiveState_SendsTitleAndArtist()
    {
        var titles = new List<string>();
        var settings = new NowPlayingSettings { MarqueeSpeed = 0 };
        using var view = new NowPlayingKeyView(
            _ => Task.CompletedTask,
            title => { titles.Add(title); return Task.CompletedTask; },
            settings);

        await view.ShowAsync(new MediaState { IsActive = true, Title = "Song", Artist = "Artist", Status = "Playing" });

        Assert.Equal(new[] { "Song\nArtist" }, titles);
    }

    [Fact]
    public async Task ConcurrentShows_NeverOverlapTitleSends()
    {
        var inFlight = 0;
        var maxInFlight = 0;
        using var view = new NowPlayingKeyView(
            _ => Task.CompletedTask,
            async _ =>
            {
                var now = Interlocked.Increment(ref inFlight);
                InterlockedMax(ref maxInFlight, now);
                await Task.Delay(20);
                Interlocked.Decrement(ref inFlight);
            },
            new NowPlayingSettings());

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => view.ShowAsync(new MediaState()))));

        Assert.Equal(1, maxInFlight);
    }

    [Fact]
    public async Task FailingSend_DoesNotThrow()
    {
        using var view = new NowPlayingKeyView(
            _ => throw new InvalidOperationException("image"),
            _ => throw new InvalidOperationException("title"),
            new NowPlayingSettings());

        await view.ShowAsync(new MediaState());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(1, 1484)]
    [InlineData(100, 500)]
    [InlineData(500, 500)]
    public void MarqueeInterval_MatchesTheOldFormula(int speed, int expectedMs)
    {
        Assert.Equal(expectedMs, MarqueeHelper.IntervalMs(speed));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (value <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
