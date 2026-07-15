using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CurrentMedia.Imaging;

static class CoverProcessor
{
    public const int TargetSize = 144;
    public const int PartSize = 72;

    public static ProcessedBitmaps? Process(string coverBase64)
    {
        using var decoded = ImageExtensions.DecodeFromBase64(coverBase64);
        if (decoded == null)
        {
            return null;
        }

        var result = new ProcessedBitmaps
        {
            SquareFull = CropToSquare(decoded, TargetSize)
        };

        SplitIntoParts(result.SquareFull!, out var square1, out var square2, out var square3, out var square4);
        result.SquarePart1 = square1;
        result.SquarePart2 = square2;
        result.SquarePart3 = square3;
        result.SquarePart4 = square4;

        result.FitFull = FitToTop(decoded, TargetSize);
        SplitIntoParts(result.FitFull!, out var fit1, out var fit2, out var fit3, out var fit4);
        result.FitPart1 = fit1;
        result.FitPart2 = fit2;
        result.FitPart3 = fit3;
        result.FitPart4 = fit4;

        return result;
    }

    private static Image<Rgba32> CropToSquare(Image<Rgba32> source, int targetSize)
    {
        return source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(targetSize, targetSize),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));
    }

    private static Image<Rgba32> FitToTop(Image<Rgba32> source, int targetSize)
    {
        var result = new Image<Rgba32>(targetSize, targetSize);
        result.Mutate(ctx => ctx.BackgroundColor(Color.Black));

        var scale = (double)targetSize / Math.Max(source.Width, source.Height);
        var scaledWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
        var offsetX = (targetSize - scaledWidth) / 2;

        using var scaled = source.Clone(ctx => ctx.Resize(scaledWidth, scaledHeight));
        result.Mutate(ctx => ctx.DrawImage(scaled, new Point(offsetX, 0), 1f));

        return result;
    }

    private static void SplitIntoParts(
        Image<Rgba32> full,
        out Image<Rgba32>? part1,
        out Image<Rgba32>? part2,
        out Image<Rgba32>? part3,
        out Image<Rgba32>? part4)
    {
        part1 = ExtractPart(full, 0, 0);
        part2 = ExtractPart(full, 1, 0);
        part3 = ExtractPart(full, 0, 1);
        part4 = ExtractPart(full, 1, 1);
    }

    private static Image<Rgba32> ExtractPart(Image<Rgba32> source, int col, int row)
    {
        var rect = new Rectangle(col * PartSize, row * PartSize, PartSize, PartSize);
        return source.Clone(ctx => ctx.Crop(rect));
    }
}
