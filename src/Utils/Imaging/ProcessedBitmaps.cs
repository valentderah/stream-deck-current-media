using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CurrentMedia.Imaging;

sealed class ProcessedBitmaps : IDisposable
{
    public Image<Rgba32>? SquareFull { get; set; }
    public Image<Rgba32>? SquarePart1 { get; set; }
    public Image<Rgba32>? SquarePart2 { get; set; }
    public Image<Rgba32>? SquarePart3 { get; set; }
    public Image<Rgba32>? SquarePart4 { get; set; }
    public Image<Rgba32>? FitFull { get; set; }
    public Image<Rgba32>? FitPart1 { get; set; }
    public Image<Rgba32>? FitPart2 { get; set; }
    public Image<Rgba32>? FitPart3 { get; set; }
    public Image<Rgba32>? FitPart4 { get; set; }
    public Image<Rgba32>? Icon { get; set; }

    public Image<Rgba32>? Get(ImagePosition position, CropMode cropMode)
    {
        var useFit = cropMode == CropMode.Fit;
        return position switch
        {
            ImagePosition.TopLeft => useFit ? FitPart1 : SquarePart1,
            ImagePosition.TopRight => useFit ? FitPart2 : SquarePart2,
            ImagePosition.BottomLeft => useFit ? FitPart3 : SquarePart3,
            ImagePosition.BottomRight => useFit ? FitPart4 : SquarePart4,
            ImagePosition.NoImage => null,
            _ => useFit ? FitFull : SquareFull
        };
    }

    public void Dispose()
    {
        SquareFull?.Dispose();
        SquarePart1?.Dispose();
        SquarePart2?.Dispose();
        SquarePart3?.Dispose();
        SquarePart4?.Dispose();
        FitFull?.Dispose();
        FitPart1?.Dispose();
        FitPart2?.Dispose();
        FitPart3?.Dispose();
        FitPart4?.Dispose();
        Icon?.Dispose();
    }
}
