namespace CurrentMedia;

public readonly record struct MediaTimeline(
    double Position,
    double Duration,
    bool IsPlaying,
    DateTimeOffset CapturedAt)
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
        return new MediaTimeline(state.Position, state.Duration, state.Status == "Playing", capturedAt);
    }
}
