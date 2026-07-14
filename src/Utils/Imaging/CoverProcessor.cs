using SkiaSharp;

namespace CurrentMedia.Imaging;

static class CoverProcessor
{
    public const int TargetSize = 144;
    public const int PartSize = 72;

    public static ProcessedBitmaps? Process(string coverBase64)
    {
        if (string.IsNullOrEmpty(coverBase64))
        {
            return null;
        }

        using var decoded = SkiaImageExtensions.DecodeFromBase64(coverBase64);
        if (decoded == null)
        {
            return null;
        }

        var result = new ProcessedBitmaps
        {
            SquareFull = CropToSquare(decoded, TargetSize)
        };

        SKBitmap sq1, sq2, sq3, sq4;
        SplitIntoParts(result.SquareFull!, out sq1, out sq2, out sq3, out sq4);
        result.SquarePart1 = sq1;
        result.SquarePart2 = sq2;
        result.SquarePart3 = sq3;
        result.SquarePart4 = sq4;

        result.FitFull = FitToTop(decoded, TargetSize);
        SKBitmap fq1, fq2, fq3, fq4;
        SplitIntoParts(result.FitFull!, out fq1, out fq2, out fq3, out fq4);
        result.FitPart1 = fq1;
        result.FitPart2 = fq2;
        result.FitPart3 = fq3;
        result.FitPart4 = fq4;

        return result;
    }

    private static SKBitmap CropToSquare(SKBitmap source, int targetSize)
    {
        var width = source.Width;
        var height = source.Height;
        var minDimension = Math.Min(width, height);
        var scale = (double)targetSize / minDimension;

        var scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(height * scale));
        var offsetX = scaledWidth > targetSize ? (scaledWidth - targetSize) / 2 : 0;
        var offsetY = scaledHeight > targetSize ? (scaledHeight - targetSize) / 2 : 0;

        var result = new SKBitmap(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        result.Erase(new SKColor(0, 0, 0, 255));

        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.Medium
        };

        var destRect = SKRect.Create(-offsetX, -offsetY, scaledWidth, scaledHeight);
        canvas.DrawBitmap(source, destRect, paint);

        return result;
    }

    private static SKBitmap FitToTop(SKBitmap source, int targetSize)
    {
        var width = source.Width;
        var height = source.Height;
        var maxDimension = Math.Max(width, height);
        var scale = (double)targetSize / maxDimension;

        var scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(height * scale));
        var offsetX = -(targetSize - scaledWidth) / 2;

        var result = new SKBitmap(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        result.Erase(new SKColor(0, 0, 0, 255));

        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.Medium
        };

        var destRect = SKRect.Create(offsetX, 0, scaledWidth, scaledHeight);
        canvas.DrawBitmap(source, destRect, paint);

        return result;
    }

    private static void SplitIntoParts(
        SKBitmap full,
        out SKBitmap part1,
        out SKBitmap part2,
        out SKBitmap part3,
        out SKBitmap part4)
    {
        part1 = ExtractPart(full, 0, 0);
        part2 = ExtractPart(full, 1, 0);
        part3 = ExtractPart(full, 0, 1);
        part4 = ExtractPart(full, 1, 1);
    }

    private static SKBitmap ExtractPart(SKBitmap source, int col, int row)
    {
        var result = new SKBitmap(PartSize, PartSize, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(result);
        using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };

        var srcRect = new SKRect(col * PartSize, row * PartSize, (col + 1) * PartSize, (row + 1) * PartSize);
        var dstRect = new SKRect(0, 0, PartSize, PartSize);
        canvas.DrawBitmap(source, srcRect, dstRect, paint);

        return result;
    }
}
