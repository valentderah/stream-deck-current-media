namespace CurrentMedia;

public class MediaState
{
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public List<string> Artists { get; set; } = new();
    public string AlbumArtist { get; set; } = "";
    public string AlbumTitle { get; set; } = "";
    public string Status { get; set; } = "Stopped";
    public bool IsActive { get; set; }
    public string CoverArtBase64 { get; set; } = "";
    public string AppIconBase64 { get; set; } = "";
    public double Position { get; set; }
    public double Duration { get; set; }

    public bool HasMediaData =>
        IsActive && (!string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Artist) || Artists.Count > 0);
}
