using SkiaSharp;

namespace CurrentMedia.Imaging;

static class SkiaImageExtensions
{
    public static SKBitmap? DecodeFromBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            return SKBitmap.Decode(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static string ToPngDataUri(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }

    public static SKBitmap CreateTransparent(int size)
    {
        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        return bitmap;
    }
}
