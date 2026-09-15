using CurrentMedia;
using CurrentMedia.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace CurrentMedia.Tests;

public class ImagePipelineIdleTests : IDisposable
{
    public void Dispose()
    {
        ImagePipeline.DisposeCache();
    }

    [Fact]
    public void NoCoverArt_WithIdleImage_UsesIdlePixels()
    {
        var idle = CreateSolidPngDataUri(144, 144, Color.Red);
        var state = new MediaState
        {
            IsActive = true,
            Title = "Song",
            Artist = "Artist",
            Status = "Playing"
        };

        var uri = ImagePipeline.RenderForPosition(
            state,
            ImagePosition.NoImage,
            CropMode.Square,
            OverlayDisplayMode.None,
            idle);

        using var image = IdleImageHelper.DecodeDataUri(uri);
        Assert.NotNull(image);
        Assert.True(image![72, 72] == new Rgba32(255, 0, 0, 255));
    }

    [Fact]
    public void NoCoverArt_WithoutIdleImage_IsTransparent()
    {
        var state = new MediaState
        {
            IsActive = true,
            Title = "Song",
            Artist = "Artist",
            Status = "Playing"
        };

        var uri = ImagePipeline.RenderForPosition(
            state,
            ImagePosition.NoImage,
            CropMode.Square,
            OverlayDisplayMode.None);

        using var image = IdleImageHelper.DecodeDataUri(uri);
        Assert.NotNull(image);
        Assert.Equal(0, image![72, 72].A);
    }

    [Fact]
    public void FullCover_IgnoresIdleImage()
    {
        var idle = CreateSolidPngDataUri(144, 144, Color.Red);
        var cover = CreateSolidPngDataUri(144, 144, Color.Blue);
        var coverB64 = cover["data:image/png;base64,".Length..];

        var state = new MediaState
        {
            IsActive = true,
            Title = "Song",
            Artist = "Artist",
            CoverArtBase64 = coverB64,
            Status = "Playing"
        };

        ImagePipeline.PrepareCache(state);
        var uri = ImagePipeline.RenderForPosition(
            state,
            ImagePosition.None,
            CropMode.Square,
            OverlayDisplayMode.None,
            idle);

        using var image = IdleImageHelper.DecodeDataUri(uri);
        Assert.NotNull(image);
        Assert.True(image![72, 72] == new Rgba32(0, 0, 255, 255));
    }

    private static string CreateSolidPngDataUri(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(color));
        return CurrentMedia.Imaging.ImageExtensions.ToPngDataUri(image);
    }
}
