using System.Collections.Generic;

namespace CurrentMedia;

public class MediaInfo
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
    public string AppIconBase64 { get; set; } = string.Empty;

    public bool HasMediaData => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Artist) || Artists.Count > 0;
}
