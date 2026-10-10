using Xunit;

namespace CurrentMedia.Tests;

public class DialGestureTests
{
    [Fact]
    public void Press_WithoutRotation_Fires()
    {
        var tracker = new DialPressTracker();

        tracker.Down();

        Assert.True(tracker.Up());
    }

    [Fact]
    public void Press_WithRotation_DoesNotFire()
    {
        var tracker = new DialPressTracker();

        tracker.Down();
        tracker.Rotated();

        Assert.False(tracker.Up());
    }

    [Fact]
    public void RotationBeforeThePress_DoesNotSuppressIt()
    {
        var tracker = new DialPressTracker();

        tracker.Rotated();
        tracker.Down();

        Assert.True(tracker.Up());
    }

    [Fact]
    public void Up_WithoutDown_DoesNotFire()
    {
        var tracker = new DialPressTracker();

        Assert.False(tracker.Up());
    }

    [Fact]
    public void SecondPress_AfterASuppressedOne_Fires()
    {
        var tracker = new DialPressTracker();
        tracker.Down();
        tracker.Rotated();
        tracker.Up();

        tracker.Down();

        Assert.True(tracker.Up());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(-1, -1)]
    [InlineData(-3, -1)]
    [InlineData(0, 0)]
    public void Throttle_FirstTick_GivesOneStepInItsDirection(int ticks, int expected)
    {
        var throttle = new TrackStepThrottle(TimeSpan.FromMilliseconds(250), new ManualTimeProvider());

        Assert.Equal(expected, throttle.Take(ticks));
    }

    [Fact]
    public void Throttle_DropsTicksInsideTheInterval()
    {
        var time = new ManualTimeProvider();
        var throttle = new TrackStepThrottle(TimeSpan.FromMilliseconds(250), time);

        Assert.Equal(1, throttle.Take(1));
        time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, throttle.Take(1));
        Assert.Equal(0, throttle.Take(-1));
        time.Advance(TimeSpan.FromMilliseconds(150));
        Assert.Equal(-1, throttle.Take(-1));
    }

    [Fact]
    public void Throttle_ZeroTicks_DoNotStartTheInterval()
    {
        var time = new ManualTimeProvider();
        var throttle = new TrackStepThrottle(TimeSpan.FromMilliseconds(250), time);

        Assert.Equal(0, throttle.Take(0));
        Assert.Equal(1, throttle.Take(1));
    }
}
