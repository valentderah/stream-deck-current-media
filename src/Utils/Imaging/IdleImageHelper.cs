using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CurrentMedia.Imaging;

static class IdleImageHelper
{
    public const int Size = CoverProcessor.TargetSize;
    private const string PngPrefix = "data:image/png;base64,";
    private const long MaxFileBytes = 15 * 1024 * 1024;

    public static Image<Rgba32>? DecodeDataUri(string? dataUri)
    {
        if (string.IsNullOrEmpty(dataUri))
        {
            return null;
        }

        var payload = dataUri;
        var comma = dataUri.IndexOf(',');
        if (dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
        {
            payload = dataUri[(comma + 1)..];
        }

        return ImageExtensions.DecodeFromBase64(payload);
    }

    public static bool IsCanonicalDataUri(string? dataUri)
    {
        if (string.IsNullOrEmpty(dataUri) ||
            !dataUri.StartsWith(PngPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var image = DecodeDataUri(dataUri);
        return image is { Width: Size, Height: Size };
    }

    public static string? NormalizeToCanonicalPng(string? dataUri)
    {
        using var decoded = DecodeDataUri(dataUri);
        if (decoded == null)
        {
            return null;
        }

        if (dataUri!.StartsWith(PngPrefix, StringComparison.OrdinalIgnoreCase) &&
            decoded is { Width: Size, Height: Size })
        {
            return dataUri;
        }

        using var cropped = CropToCanonical(decoded);
        return ImageExtensions.ToPngDataUri(cropped);
    }

    public static string? TryLoadFromFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var length = new FileInfo(path).Length;
            if (length <= 0 || length > MaxFileBytes)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var decoded = Image.Load<Rgba32>(stream);
            using var cropped = CropToCanonical(decoded);
            return ImageExtensions.ToPngDataUri(cropped);
        }
        catch
        {
            return null;
        }
    }

    private static Image<Rgba32> CropToCanonical(Image<Rgba32> source)
    {
        return source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(Size, Size),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center
        }));
    }
}
