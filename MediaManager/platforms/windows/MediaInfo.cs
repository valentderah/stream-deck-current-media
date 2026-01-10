using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace MediaManager.Windows;

class MediaInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new List<string>();
    public string AlbumArtist { get; set; } = string.Empty;
    public string AlbumTitle { get; set; } = string.Empty;
    public string Status { get; set; } = "Stopped";
    public string CoverArtBase64 { get; set; } = string.Empty;
    public string CoverArtPart1Base64 { get; set; } = string.Empty;
    public string CoverArtPart2Base64 { get; set; } = string.Empty;
    public string CoverArtPart3Base64 { get; set; } = string.Empty;
    public string CoverArtPart4Base64 { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(MediaInfo))]
internal partial class MediaInfoJsonContext : JsonSerializerContext
{
}
