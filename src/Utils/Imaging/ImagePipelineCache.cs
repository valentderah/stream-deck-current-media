using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CurrentMedia.Imaging;

sealed class ImagePipelineCache : IDisposable
{
    private readonly object _lock = new();
    private ProcessedBitmaps? _bitmaps;
    private string _coverKey = "";
    private string _iconKey = "";

    public void Update(string coverBase64, string iconBase64)
    {
        lock (_lock)
        {
            var coverChanged = _coverKey != coverBase64;
            var iconChanged = _iconKey != iconBase64;

            if (coverChanged)
            {
                _bitmaps?.Dispose();
                _bitmaps = CoverProcessor.Process(coverBase64);
                _coverKey = coverBase64;
            }

            if (coverChanged || iconChanged)
            {
                _bitmaps?.Icon?.Dispose();
                if (_bitmaps != null)
                {
                    _bitmaps.Icon = ImageExtensions.DecodeFromBase64(iconBase64);
                }

                _iconKey = iconBase64;
            }
        }
    }

    public Image<Rgba32>? CloneIcon(bool includeIcon)
    {
        lock (_lock)
        {
            if (!includeIcon || _bitmaps?.Icon == null)
            {
                return null;
            }

            return ImageExtensions.CloneImage(_bitmaps.Icon);
        }
    }

    public (Image<Rgba32> Cover, Image<Rgba32>? Icon) CloneCoverAndIcon(
        ImagePosition position,
        CropMode cropMode,
        int size,
        bool includeIcon)
    {
        lock (_lock)
        {
            var cached = _bitmaps?.Get(position, cropMode);
            var cover = cached == null
                ? ImageExtensions.CreateTransparent(size)
                : ImageExtensions.CloneImage(cached);
            var icon = includeIcon && _bitmaps?.Icon != null
                ? ImageExtensions.CloneImage(_bitmaps.Icon)
                : null;
            return (cover, icon);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _bitmaps?.Dispose();
            _bitmaps = null;
            _coverKey = "";
            _iconKey = "";
        }
    }
}
