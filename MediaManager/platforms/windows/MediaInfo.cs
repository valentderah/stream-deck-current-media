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
    public string CoverArtFitBase64 { get; set; } = string.Empty;
    public string CoverArtFitPart1Base64 { get; set; } = string.Empty;
    public string CoverArtFitPart2Base64 { get; set; } = string.Empty;
    public string CoverArtFitPart3Base64 { get; set; } = string.Empty;
    public string CoverArtFitPart4Base64 { get; set; } = string.Empty;
    public string AppIconBase64 { get; set; } = string.Empty;

    public bool HasMediaData => !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Artist) || Artists.Count > 0;

    public string GetCoverArt(ImagePosition position, CropMode cropMode)
    {
        var useFit = cropMode == CropMode.Fit;
        return position switch
        {
            ImagePosition.TopLeft => useFit ? CoverArtFitPart1Base64 : CoverArtPart1Base64,
            ImagePosition.TopRight => useFit ? CoverArtFitPart2Base64 : CoverArtPart2Base64,
            ImagePosition.BottomLeft => useFit ? CoverArtFitPart3Base64 : CoverArtPart3Base64,
            ImagePosition.BottomRight => useFit ? CoverArtFitPart4Base64 : CoverArtPart4Base64,
            _ => useFit ? CoverArtFitBase64 : CoverArtBase64
        };
    }
}
