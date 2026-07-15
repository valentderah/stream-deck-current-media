using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CurrentMedia.Imaging;

static class ImageExtensions
{
    public static Image<Rgba32>? DecodeFromBase64(string base64)
    {
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            return Image.Load<Rgba32>(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static Image<Rgba32> CreateTransparent(int size)
    {
        var image = new Image<Rgba32>(size, size);
        image.Mutate(ctx => ctx.BackgroundColor(Color.Transparent));
        return image;
    }

    public static string ToPngDataUri(Image<Rgba32> image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, PngFormat.Instance);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    public static Image<Rgba32> CloneImage(Image<Rgba32> source)
    {
        return source.Clone();
    }
}
