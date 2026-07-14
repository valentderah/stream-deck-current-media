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
                    _bitmaps.Icon = SkiaImageExtensions.DecodeFromBase64(iconBase64);
                }

                _iconKey = iconBase64;
            }
        }
    }

    public T RunWithBitmaps<T>(Func<ProcessedBitmaps?, T> action)
    {
        lock (_lock)
        {
            return action(_bitmaps);
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
