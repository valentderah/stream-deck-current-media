using Xunit;

namespace CurrentMedia.Tests;

public class KeyHoldTrackerTests
{
    [Fact]
    public void ReleaseBeforeTheHold_IsAShortPress()
    {
        var tracker = new KeyHoldTracker();

        tracker.Down();

        Assert.True(tracker.Up());
    }

    [Fact]
    public void Hold_FiresOnceAndSuppressesTheRelease()
    {
        var tracker = new KeyHoldTracker();

        var press = tracker.Down();

        Assert.True(tracker.Hold(press));
        Assert.False(tracker.Hold(press));
        Assert.False(tracker.Up());
    }

    [Fact]
    public void Hold_AfterTheRelease_DoesNotFire()
    {
        var tracker = new KeyHoldTracker();
        var press = tracker.Down();
        tracker.Up();

        Assert.False(tracker.Hold(press));
    }

    [Fact]
    public void Hold_FromAnEarlierPress_DoesNotFireOnTheCurrentOne()
    {
        var tracker = new KeyHoldTracker();
        var first = tracker.Down();
        tracker.Up();

        var second = tracker.Down();

        Assert.False(tracker.Hold(first));
        Assert.True(tracker.Hold(second));
    }

    [Fact]
    public void Up_WithoutDown_DoesNotFire()
    {
        var tracker = new KeyHoldTracker();

        Assert.False(tracker.Up());
    }

    [Fact]
    public void PressAfterAHold_IsShortAgain()
    {
        var tracker = new KeyHoldTracker();
        tracker.Hold(tracker.Down());
        tracker.Up();

        tracker.Down();

        Assert.True(tracker.Up());
    }
}
