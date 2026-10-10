namespace CurrentMedia;

internal static class PlaybackClock
{
    public static double PositionAt(MediaTimeline timeline, DateTimeOffset now)
    {
        var position = timeline.Position;
        if (timeline.IsPlaying)
        {
            position += Math.Max(0, (now - timeline.CapturedAt).TotalSeconds);
        }

        return Clamp(position, timeline);
    }

    public static MediaTimeline Shift(MediaTimeline timeline, double offsetSeconds, DateTimeOffset now) =>
        MoveTo(timeline, PositionAt(timeline, now) + offsetSeconds, now);

    public static MediaTimeline MoveTo(MediaTimeline timeline, double position, DateTimeOffset now) =>
        timeline with { Position = Clamp(position, timeline), CapturedAt = now };

    private static double Clamp(double position, MediaTimeline timeline)
    {
        if (!double.IsFinite(position) || position < 0)
        {
            return 0;
        }

        return timeline.HasDuration && position > timeline.Duration ? timeline.Duration : position;
    }
}
