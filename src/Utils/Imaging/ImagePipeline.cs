using SkiaSharp;

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
        OverlayDisplayMode overlayMode)
    {
        return _cache.RunWithBitmaps(bitmaps =>
        {
            var size = position is ImagePosition.None or ImagePosition.NoImage
                ? ImageSizeFull
                : ImageSizeSingleCell;

            SKBitmap baseBitmap;
            if (position == ImagePosition.NoImage || bitmaps == null)
            {
                baseBitmap = SkiaImageExtensions.CreateTransparent(size);
            }
            else
            {
                var cached = bitmaps.Get(position, cropMode);
                if (cached == null)
                {
                    baseBitmap = SkiaImageExtensions.CreateTransparent(size);
                }
                else
                {
                    baseBitmap = cached.Copy();
                }
            }

            using (baseBitmap)
            using (var withOverlay = OverlayRenderer.Apply(baseBitmap, state, overlayMode, bitmaps?.Icon))
            {
                return SkiaImageExtensions.ToPngDataUri(withOverlay);
            }
        });
    }
}
