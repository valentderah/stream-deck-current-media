using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CurrentMedia.Imaging;

public static class ImagePipeline
{
    private static readonly ImagePipelineCache _cache = new();

    public const int TargetSize = CoverProcessor.TargetSize;
    public const int PartSize = CoverProcessor.PartSize;

    private const int ImageSizeFull = TargetSize;
    private const int ImageSizeSingleCell = PartSize;

    public static void PrepareCache(MediaState state)
    {
        _cache.Update(state.CoverArtBase64, state.AppIconBase64);
    }

    public static void DisposeCache()
    {
        _cache.Dispose();
    }

    public static string RenderForPosition(
        MediaState state,
        ImagePosition position,
        CropMode cropMode,
        OverlayDisplayMode overlayMode,
        string? idleImage = null)
    {
        Image<Rgba32>? idleBitmap = null;
        if (position == ImagePosition.NoImage)
        {
            idleBitmap = IdleImageHelper.DecodeDataUri(idleImage);
        }

        var includeIcon = overlayMode is OverlayDisplayMode.Icon or OverlayDisplayMode.Both;
        var size = position is ImagePosition.None or ImagePosition.NoImage
            ? ImageSizeFull
            : ImageSizeSingleCell;

        Image<Rgba32>? iconClone = null;
        Image<Rgba32> baseBitmap;
        try
        {
            if (position == ImagePosition.NoImage)
            {
                iconClone = _cache.CloneIcon(includeIcon);
                baseBitmap = idleBitmap ?? ImageExtensions.CreateTransparent(size);
                idleBitmap = null;
            }
            else
            {
                (baseBitmap, iconClone) = _cache.CloneCoverAndIcon(position, cropMode, size, includeIcon);
            }

            using (baseBitmap)
            using (iconClone)
            using (var withOverlay = OverlayRenderer.Apply(baseBitmap, state, overlayMode, iconClone))
            {
                return ImageExtensions.ToPngDataUri(withOverlay);
            }
        }
        finally
        {
            idleBitmap?.Dispose();
        }
    }
}
