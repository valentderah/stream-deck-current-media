namespace CurrentMedia;

internal sealed class SeekSeries
{
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private DateTimeOffset _last;
    private bool _any;

    public SeekSeries(TimeSpan window, TimeProvider time)
    {
        _window = window;
        _time = time;
    }

    public bool Next()
    {
        var now = _time.GetUtcNow();
        lock (_lock)
        {
            var continues = _any && now - _last < _window;
            _any = true;
            _last = now;
            return continues;
        }
    }
}
