namespace CurrentMedia;

internal sealed class TimelineTracker
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(2);

    private const double HoldToleranceSeconds = 1.5;
    private const double DurationToleranceSeconds = 0.5;

    private DateTimeOffset _holdUntil;

    public MediaTimeline Current { get; private set; }

    public void Accept(MediaTimeline incoming, DateTimeOffset now)
    {
        if (now < _holdUntil && IsSameTrack(incoming) && Drift(incoming, now) > HoldToleranceSeconds)
        {
            Current = new MediaTimeline(
                PlaybackClock.PositionAt(Current, now),
                Current.Duration,
                incoming.IsPlaying,
                now);
            return;
        }

        Current = incoming;
    }

    public void Shift(double offsetSeconds, DateTimeOffset now)
    {
        Current = PlaybackClock.Shift(Current, offsetSeconds, now);
        _holdUntil = now + HoldDuration;
    }

    public void Jump(double position, DateTimeOffset now)
    {
        Current = PlaybackClock.MoveTo(Current, position, now);
        _holdUntil = now + HoldDuration;
    }

    public void ReleaseHold() => _holdUntil = default;

    private bool IsSameTrack(MediaTimeline incoming) =>
        Math.Abs(incoming.Duration - Current.Duration) <= DurationToleranceSeconds;

    private double Drift(MediaTimeline incoming, DateTimeOffset now) =>
        Math.Abs(PlaybackClock.PositionAt(incoming, now) - PlaybackClock.PositionAt(Current, now));
}
