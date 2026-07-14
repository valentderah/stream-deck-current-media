using SkiaSharp;

namespace CurrentMedia.Imaging;

sealed class ProcessedBitmaps : IDisposable
{
    public SKBitmap? SquareFull { get; set; }
    public SKBitmap? SquarePart1 { get; set; }
    public SKBitmap? SquarePart2 { get; set; }
    public SKBitmap? SquarePart3 { get; set; }
    public SKBitmap? SquarePart4 { get; set; }
    public SKBitmap? FitFull { get; set; }
    public SKBitmap? FitPart1 { get; set; }
    public SKBitmap? FitPart2 { get; set; }
    public SKBitmap? FitPart3 { get; set; }
    public SKBitmap? FitPart4 { get; set; }
    public SKBitmap? Icon { get; set; }

    public SKBitmap? Get(ImagePosition position, CropMode cropMode)
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
