using Xunit;

namespace CurrentMedia.Tests;

public class TimelineTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TimelineTracker TrackerAt(double position, double duration = 200, bool playing = true)
    {
        var tracker = new TimelineTracker();
        tracker.Accept(new MediaTimeline(position, duration, playing, T0), T0);
        return tracker;
    }

    [Fact]
    public void Accept_WithoutHold_TakesTheSnapshot()
    {
        var tracker = TrackerAt(30);
        var next = new MediaTimeline(90, 200, true, T0.AddSeconds(1));

        tracker.Accept(next, T0.AddSeconds(1));

        Assert.Equal(next, tracker.Current);
    }

    [Fact]
    public void Shift_MovesTheLocalPosition()
    {
        var tracker = TrackerAt(30);

        tracker.Shift(10, T0.AddSeconds(1));

        Assert.Equal(41, tracker.Current.Position);
    }

    [Fact]
    public void Accept_StaleSnapshotDuringHold_KeepsLocalPosition()
    {
        var tracker = TrackerAt(30);
        tracker.Shift(20, T0.AddSeconds(1));

        tracker.Accept(new MediaTimeline(31.5, 200, true, T0.AddSeconds(1.5)), T0.AddSeconds(1.5));

        Assert.Equal(51.5, PlaybackClock.PositionAt(tracker.Current, T0.AddSeconds(1.5)), 3);
    }

    [Fact]
    public void Accept_StaleSnapshotDuringHold_StillTakesThePlayState()
    {
        var tracker = TrackerAt(30);
        tracker.Shift(20, T0.AddSeconds(1));

        tracker.Accept(new MediaTimeline(31.5, 200, false, T0.AddSeconds(1.5)), T0.AddSeconds(1.5));

        Assert.False(tracker.Current.IsPlaying);
        Assert.Equal(51.5, tracker.Current.Position, 3);
    }

    [Fact]
    public void Accept_StaleSnapshotDuringHold_TakesItsPlaybackRate()
    {
        var tracker = TrackerAt(30);
        tracker.Shift(20, T0.AddSeconds(1));

        tracker.Accept(new MediaTimeline(31.5, 200, true, T0.AddSeconds(1.5), 2), T0.AddSeconds(1.5));

        Assert.Equal(2, tracker.Current.Rate);
        Assert.Equal(51.5, tracker.Current.Position, 3);
    }

    [Fact]
    public void Accept_MatchingSnapshotDuringHold_TakesTheSnapshot()
    {
        var tracker = TrackerAt(30);
        tracker.Shift(20, T0.AddSeconds(1));
        var confirmed = new MediaTimeline(51.2, 200, true, T0.AddSeconds(1.5));

        tracker.Accept(confirmed, T0.AddSeconds(1.5));

        Assert.Equal(confirmed, tracker.Current);
    }

    [Fact]
    public void Accept_AfterHoldExpires_TakesTheSnapshot()
    {
        var tracker = TrackerAt(30);
        tracker.Shift(20, T0.AddSeconds(1));
        var late = new MediaTimeline(33.1, 200, true, T0.AddSeconds(3.1));

        tracker.Accept(late, T0.AddSeconds(3.1));

        Assert.Equal(late, tracker.Current);
    }

    [Fact]
    public void Accept_DifferentDuration_BypassesHold()
    {
        var tracker = TrackerAt(30);
        tracker.Shift(20, T0.AddSeconds(1));
        var newTrack = new MediaTimeline(0, 311, true, T0.AddSeconds(1.2));

        tracker.Accept(newTrack, T0.AddSeconds(1.2));

        Assert.Equal(newTrack, tracker.Current);
    }

    [Fact]
    public void ReleaseHold_LetsTheNextSnapshotThrough()
    {
        var tracker = TrackerAt(30);
        tracker.Jump(150, T0.AddSeconds(1));
        tracker.ReleaseHold();
        var snapshot = new MediaTimeline(2, 200, true, T0.AddSeconds(1.2));

        tracker.Accept(snapshot, T0.AddSeconds(1.2));

        Assert.Equal(snapshot, tracker.Current);
    }

    [Fact]
    public void Jump_SetsAnAbsolutePositionAndClamps()
    {
        var tracker = TrackerAt(30);

        tracker.Jump(500, T0.AddSeconds(1));

        Assert.Equal(200, tracker.Current.Position);
    }
}
