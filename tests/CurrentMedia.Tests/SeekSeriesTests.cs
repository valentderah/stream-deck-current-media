using Xunit;

namespace CurrentMedia.Tests;

public class SeekSeriesTests
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);

    [Fact]
    public void Next_FirstSeek_StartsASeries()
    {
        var series = new SeekSeries(Window, new ManualTimeProvider());

        Assert.False(series.Next());
    }

    [Fact]
    public void Next_InsideTheWindow_ContinuesTheSeries()
    {
        var time = new ManualTimeProvider();
        var series = new SeekSeries(Window, time);
        series.Next();

        time.Advance(TimeSpan.FromMilliseconds(400));

        Assert.True(series.Next());
    }

    [Fact]
    public void Next_EachSeekExtendsTheWindow()
    {
        var time = new ManualTimeProvider();
        var series = new SeekSeries(Window, time);
        series.Next();

        time.Advance(TimeSpan.FromMilliseconds(1500));
        Assert.True(series.Next());
        time.Advance(TimeSpan.FromMilliseconds(1500));

        Assert.True(series.Next());
    }

    [Fact]
    public void Next_AfterTheWindow_StartsANewSeries()
    {
        var time = new ManualTimeProvider();
        var series = new SeekSeries(Window, time);
        series.Next();

        time.Advance(Window);

        Assert.False(series.Next());
    }
}
