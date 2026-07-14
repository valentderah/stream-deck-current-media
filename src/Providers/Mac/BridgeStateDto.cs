using System.Text.Json.Serialization;

namespace CurrentMedia.Mac;

internal sealed class BridgeStateDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = "";

    [JsonPropertyName("albumTitle")]
    public string AlbumTitle { get; set; } = "";

    [JsonPropertyName("position")]
    public double Position { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("bundleId")]
    public string BundleId { get; set; } = "";

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("coverBase64")]
    public string CoverBase64 { get; set; } = "";

    [JsonPropertyName("sourceIconBase64")]
    public string SourceIconBase64 { get; set; } = "";

    public MediaState ToMediaState() => new()
    {
        Title = Title,
        Artist = Artist,
        Artists = string.IsNullOrEmpty(Artist)
            ? new List<string>()
            : Artist.Split(',', StringSplitOptions.TrimEntries).ToList(),
        AlbumTitle = AlbumTitle,
        Status = State switch
        {
            "playing" => "Playing",
            "paused" => "Paused",
            _ => "Stopped"
        },
        IsActive = IsActive,
        CoverArtBase64 = CoverBase64,
        AppIconBase64 = SourceIconBase64,
        Position = Position,
        Duration = Duration
    };
}
