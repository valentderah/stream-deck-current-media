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

    [Fact]
    public void StripCover_WithCover_IsTheCoverAtStripSize()
    {
        var cover = CreateSolidPngDataUri(300, 300, Color.Blue);
        var state = new MediaState
        {
            IsActive = true,
            Title = "Song",
            CoverArtBase64 = cover["data:image/png;base64,".Length..],
            Status = "Playing"
        };
        ImagePipeline.PrepareCache(state);

        using var image = IdleImageHelper.DecodeDataUri(ImagePipeline.RenderStripCover(true));

        Assert.NotNull(image);
        Assert.Equal(56, image!.Width);
        Assert.Equal(56, image.Height);
        Assert.True(image[28, 28] == new Rgba32(0, 0, 255, 255));
    }

    [Fact]
    public void StripCover_MediaWithoutArt_IsTransparent()
    {
        ImagePipeline.PrepareCache(new MediaState { IsActive = true, Title = "Song", Status = "Playing" });

        using var image = IdleImageHelper.DecodeDataUri(ImagePipeline.RenderStripCover(true));

        Assert.NotNull(image);
        Assert.Equal(56, image!.Width);
        Assert.Equal(0, image[28, 28].A);
    }

    [Fact]
    public void StripCover_Idle_IsTransparent()
    {
        using var image = IdleImageHelper.DecodeDataUri(ImagePipeline.RenderStripCover(false));

        Assert.NotNull(image);
        Assert.Equal(56, image!.Width);
        Assert.Equal(0, image[28, 28].A);
    }

    [Theory]
    [InlineData(144, 144)]
    [InlineData(200, 100)]
    public void StripBackground_Idle_FillsTheWholeSegment(int width, int height)
    {
        using var image = IdleImageHelper.DecodeDataUri(
            ImagePipeline.RenderStripBackground(false, CreateSolidPngDataUri(width, height, Color.Red)));

        Assert.NotNull(image);
        Assert.Equal(TouchZones.StripWidth, image!.Width);
        Assert.Equal(TouchZones.StripHeight, image.Height);
        Assert.True(image[0, 0] == new Rgba32(255, 0, 0, 255));
        Assert.True(image[199, 99] == new Rgba32(255, 0, 0, 255));
    }

    [Fact]
    public void StripBackground_WithMedia_IsTransparent()
    {
        using var image = IdleImageHelper.DecodeDataUri(
            ImagePipeline.RenderStripBackground(true, CreateSolidPngDataUri(200, 100, Color.Red)));

        Assert.NotNull(image);
        Assert.Equal(TouchZones.StripWidth, image!.Width);
        Assert.Equal(TouchZones.StripHeight, image.Height);
        Assert.Equal(0, image[100, 50].A);
    }

    [Fact]
    public void StripBackground_IdleWithoutImage_IsTransparent()
    {
        using var image = IdleImageHelper.DecodeDataUri(ImagePipeline.RenderStripBackground(false, null));

        Assert.NotNull(image);
        Assert.Equal(TouchZones.StripWidth, image!.Width);
        Assert.Equal(0, image[100, 50].A);
    }

    private static string CreateSolidPngDataUri(int width, int height, Color color)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(color));
        return CurrentMedia.Imaging.ImageExtensions.ToPngDataUri(image);
    }
}
