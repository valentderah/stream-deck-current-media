using Xunit;

namespace CurrentMedia.Tests;

public class PlaybackClockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PositionAt_Playing_AdvancesWithTheClock()
    {
        var timeline = new MediaTimeline(30, 200, true, T0);

        Assert.Equal(35, PlaybackClock.PositionAt(timeline, T0.AddSeconds(5)));
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(0.5, 32.5)]
    public void PositionAt_Playing_AdvancesAtThePlaybackRate(double rate, double expected)
    {
        var timeline = new MediaTimeline(30, 200, true, T0, rate);

        Assert.Equal(expected, PlaybackClock.PositionAt(timeline, T0.AddSeconds(5)));
    }

    [Fact]
    public void Shift_KeepsThePlaybackRate()
    {
        var timeline = new MediaTimeline(30, 200, true, T0, 2);

        var shifted = PlaybackClock.Shift(timeline, 10, T0.AddSeconds(5));

        Assert.Equal(new MediaTimeline(50, 200, true, T0.AddSeconds(5), 2), shifted);
    }

    [Fact]
    public void From_CopiesThePlaybackRate()
    {
        var state = new MediaState { IsActive = true, Status = "Playing", Duration = 100, PlaybackRate = 1.5 };

        Assert.Equal(1.5, MediaTimeline.From(state, T0).Rate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void From_UnusablePlaybackRate_IsNormalSpeed(double rate)
    {
        var state = new MediaState { IsActive = true, Status = "Playing", Duration = 100, PlaybackRate = rate };

        Assert.Equal(1, MediaTimeline.From(state, T0).Rate);
    }

    [Fact]
    public void PositionAt_Paused_StaysPut()
    {
        var timeline = new MediaTimeline(30, 200, false, T0);

        Assert.Equal(30, PlaybackClock.PositionAt(timeline, T0.AddSeconds(5)));
    }

    [Fact]
    public void PositionAt_ClampsAtDuration()
    {
        var timeline = new MediaTimeline(190, 200, true, T0);

        Assert.Equal(200, PlaybackClock.PositionAt(timeline, T0.AddSeconds(60)));
    }

    [Fact]
    public void PositionAt_ZeroDuration_DoesNotClampTheTop()
    {
        var timeline = new MediaTimeline(30, 0, true, T0);

        Assert.Equal(40, PlaybackClock.PositionAt(timeline, T0.AddSeconds(10)));
    }

    [Fact]
    public void PositionAt_ClockBehindTheSnapshot_DoesNotGoBackwards()
    {
        var timeline = new MediaTimeline(30, 200, true, T0);

        Assert.Equal(30, PlaybackClock.PositionAt(timeline, T0.AddSeconds(-5)));
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void PositionAt_BadPosition_IsZero(double position)
    {
        var timeline = new MediaTimeline(position, 200, false, T0);

        Assert.Equal(0, PlaybackClock.PositionAt(timeline, T0));
    }

    [Fact]
    public void Shift_MovesFromTheCurrentPositionAndReanchors()
    {
        var timeline = new MediaTimeline(30, 200, true, T0);

        var shifted = PlaybackClock.Shift(timeline, 10, T0.AddSeconds(5));

        Assert.Equal(new MediaTimeline(45, 200, true, T0.AddSeconds(5)), shifted);
    }

    [Fact]
    public void Shift_ClampsAtBothEnds()
    {
        var timeline = new MediaTimeline(30, 200, false, T0);

        Assert.Equal(0, PlaybackClock.Shift(timeline, -100, T0).Position);
        Assert.Equal(200, PlaybackClock.Shift(timeline, 1000, T0).Position);
    }

    [Fact]
    public void MoveTo_SetsAnAbsolutePosition()
    {
        var timeline = new MediaTimeline(30, 200, true, T0);

        Assert.Equal(new MediaTimeline(120, 200, true, T0.AddSeconds(2)), PlaybackClock.MoveTo(timeline, 120, T0.AddSeconds(2)));
    }

    [Fact]
    public void From_ActiveState_CopiesTheFields()
    {
        var state = new MediaState
        {
            IsActive = true,
            Status = "Playing",
            Position = 12,
            Duration = 100,
            PositionCapturedAt = T0
        };

        Assert.Equal(new MediaTimeline(12, 100, true, T0), MediaTimeline.From(state, T0.AddSeconds(9)));
    }

    [Fact]
    public void From_StateWithoutCaptureTime_UsesNow()
    {
        var state = new MediaState { IsActive = true, Status = "Paused", Position = 12, Duration = 100 };

        Assert.Equal(T0, MediaTimeline.From(state, T0).CapturedAt);
    }

    [Fact]
    public void From_InactiveState_IsNone()
    {
        Assert.Equal(MediaTimeline.None, MediaTimeline.From(new MediaState { Position = 5, Duration = 9 }, T0));
        Assert.False(MediaTimeline.None.HasDuration);
    }
}
