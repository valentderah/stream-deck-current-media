using System.Text.Json.Serialization;

namespace CurrentMedia.Mac;

internal sealed class AdapterStreamMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("payload")]
    public AdapterPayload? Payload { get; set; }
}

internal sealed class AdapterPayload
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = "";

    [JsonPropertyName("album")]
    public string Album { get; set; } = "";

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("elapsedTime")]
    public double ElapsedTime { get; set; }

    [JsonPropertyName("durationMicros")]
    public double? DurationMicros { get; set; }

    [JsonPropertyName("elapsedTimeMicros")]
    public double? ElapsedTimeMicros { get; set; }

    [JsonPropertyName("timestampEpochMicros")]
    public double? TimestampEpochMicros { get; set; }

    [JsonPropertyName("playing")]
    public bool Playing { get; set; }

    [JsonPropertyName("playbackRate")]
    public double PlaybackRate { get; set; }

    [JsonPropertyName("bundleIdentifier")]
    public string BundleIdentifier { get; set; } = "";

    [JsonPropertyName("parentApplicationBundleIdentifier")]
    public string ParentApplicationBundleIdentifier { get; set; } = "";

    [JsonPropertyName("artworkData")]
    public string ArtworkData { get; set; } = "";

    private const double MicrosPerSecond = 1_000_000;

    public MediaState ToMediaState(string appIconBase64, DateTimeOffset now)
    {
        var hasTrackData = !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Artist);
        var isPlaying = Playing || PlaybackRate > 0;

        return new MediaState
        {
            Title = Title,
            Artist = Artist,
            Artists = string.IsNullOrEmpty(Artist)
                ? new List<string>()
                : Artist.Split(',', StringSplitOptions.TrimEntries).ToList(),
            AlbumTitle = Album,
            Status = isPlaying
                ? "Playing"
                : hasTrackData
                    ? "Paused"
                    : "Stopped",
            IsActive = hasTrackData,
            CoverArtBase64 = MacArtworkConverter.NormalizeToDisplayBase64(ArtworkData),
            AppIconBase64 = appIconBase64,
            Position = ElapsedTimeMicros / MicrosPerSecond ?? ElapsedTime,
            Duration = DurationMicros / MicrosPerSecond ?? Duration,
            PositionCapturedAt = CapturedAt(now),
            PlaybackRate = PlaybackRate
        };
    }

    // elapsedTime is only valid at the moment the player stamped it, not when the update arrives.
    private DateTimeOffset CapturedAt(DateTimeOffset now)
    {
        if (TimestampEpochMicros is not { } micros || !double.IsFinite(micros) || micros <= 0)
        {
            return now;
        }

        return DateTimeOffset.UnixEpoch.AddTicks((long)(micros * (TimeSpan.TicksPerSecond / MicrosPerSecond)));
    }
}
