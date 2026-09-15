using CurrentMedia.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace CurrentMedia.Tests;

public class IdleImageHelperTests
{
    [Fact]
    public void DecodeDataUri_Empty_ReturnsNull()
    {
        Assert.Null(IdleImageHelper.DecodeDataUri(""));
        Assert.Null(IdleImageHelper.DecodeDataUri(null));
        Assert.False(IdleImageHelper.IsCanonicalDataUri(""));
        Assert.Null(IdleImageHelper.NormalizeToCanonicalPng(""));
    }

    [Fact]
    public void DecodeDataUri_Garbage_ReturnsNull()
    {
        Assert.Null(IdleImageHelper.DecodeDataUri("not-an-image"));
        Assert.Null(IdleImageHelper.NormalizeToCanonicalPng("data:image/png;base64,@@@@"));
    }

    [Fact]
    public void Normalize_WideJpeg_BecomesCanonicalPng()
    {
        var jpegUri = CreateSolidJpegDataUri(200, 100, Color.Red);
        Assert.False(IdleImageHelper.IsCanonicalDataUri(jpegUri));

        var normalized = IdleImageHelper.NormalizeToCanonicalPng(jpegUri);
        Assert.NotNull(normalized);
        Assert.StartsWith("data:image/png;base64,", normalized);
        Assert.True(IdleImageHelper.IsCanonicalDataUri(normalized));

        using var image = IdleImageHelper.DecodeDataUri(normalized);
        Assert.NotNull(image);
        Assert.Equal(144, image!.Width);
        Assert.Equal(144, image.Height);
        Assert.True(image[72, 72].R > 200 && image[72, 72].G < 50);
    }

    [Fact]
    public void IsCanonical_144Png_UnchangedByNormalize()
    {
        var pngUri = CreateSolidPngDataUri(144, 144, Color.Blue);
        Assert.True(IdleImageHelper.IsCanonicalDataUri(pngUri));
        Assert.Equal(pngUri, IdleImageHelper.NormalizeToCanonicalPng(pngUri));
    }

    [Fact]
    public void IsCanonical_144Jpeg_IsFalse()
    {
        var jpegUri = CreateSolidJpegDataUri(144, 144, Color.Green);
        Assert.False(IdleImageHelper.IsCanonicalDataUri(jpegUri));
        var normalized = IdleImageHelper.NormalizeToCanonicalPng(jpegUri);
        Assert.NotNull(normalized);
        Assert.True(IdleImageHelper.IsCanonicalDataUri(normalized));
    }

    [Fact]
    public void TryLoadFromFile_Missing_ReturnsNull()
    {
        Assert.Null(IdleImageHelper.TryLoadFromFile(null));
        Assert.Null(IdleImageHelper.TryLoadFromFile(""));
        Assert.Null(IdleImageHelper.TryLoadFromFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png")));
    }

    [Fact]
    public void TryLoadFromFile_Jpeg_BecomesCanonicalPng()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        try
        {
            using (var image = new Image<Rgba32>(200, 100))
            {
                image.Mutate(ctx => ctx.BackgroundColor(Color.Red));
                image.Save(path, new JpegEncoder { Quality = 95 });
            }

            var normalized = IdleImageHelper.TryLoadFromFile(path);
            Assert.NotNull(normalized);
            Assert.True(IdleImageHelper.IsCanonicalDataUri(normalized));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string CreateSolidPngDataUri(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(color));
        using var ms = new MemoryStream();
        image.Save(ms, PngFormat.Instance);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }

    private static string CreateSolidJpegDataUri(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(color));
        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 95 });
        return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
    }
}
