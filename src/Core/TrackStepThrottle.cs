namespace CurrentMedia;

internal sealed class TrackStepThrottle
{
    private readonly TimeSpan _minInterval;
    private readonly TimeProvider _time;
    private DateTimeOffset _last;
    private bool _any;

    public TrackStepThrottle(TimeSpan minInterval, TimeProvider time)
    {
        _minInterval = minInterval;
        _time = time;
    }

    public int Take(int ticks)
    {
        if (ticks == 0)
        {
            return 0;
        }

        var now = _time.GetUtcNow();
        if (_any && now - _last < _minInterval)
        {
            return 0;
        }

        _any = true;
        _last = now;
        return Math.Sign(ticks);
    }
}
