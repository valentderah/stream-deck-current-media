namespace CurrentMedia;

public readonly record struct MediaTimeline(
    double Position,
    double Duration,
    bool IsPlaying,
    DateTimeOffset CapturedAt,
    double Rate = 1)
{
    public static MediaTimeline None => default;

    public bool HasDuration => Duration > 0 && double.IsFinite(Duration);

    public static MediaTimeline From(MediaState state, DateTimeOffset now)
    {
        if (!state.IsActive)
        {
            return None;
        }

        var capturedAt = state.PositionCapturedAt == default ? now : state.PositionCapturedAt;
        return new MediaTimeline(
            state.Position,
            state.Duration,
            state.Status == "Playing",
            capturedAt,
            NormalizeRate(state.PlaybackRate));
    }

    // Sources that do not report a speed leave it at zero.
    public static double NormalizeRate(double rate) => double.IsFinite(rate) && rate > 0 ? rate : 1;
}
