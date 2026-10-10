using System.Diagnostics;

namespace CurrentMedia;

internal sealed class SeekAccumulator : IDisposable
{
    private static readonly TimeSpan DefaultQuietPeriod = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMilliseconds(400);

    private readonly Func<int, Task> _flush;
    private readonly TimeSpan _quietPeriod;
    private readonly TimeSpan _maxDelay;
    private readonly Action<Exception>? _onError;
    private readonly object _lock = new();
    private readonly Timer _timer;
    private long _pending;
    private long _burstStart;
    private bool _burstActive;
    private bool _disposed;

    public SeekAccumulator(
        Func<int, Task> flush,
        TimeSpan? quietPeriod = null,
        TimeSpan? maxDelay = null,
        Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(flush);

        _flush = flush;
        _quietPeriod = quietPeriod ?? DefaultQuietPeriod;
        _maxDelay = maxDelay ?? DefaultMaxDelay;
        _onError = onError;
        _timer = new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Add(int seconds)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _pending += seconds;
            if (!_burstActive)
            {
                _burstActive = true;
                _burstStart = Stopwatch.GetTimestamp();
            }

            var remaining = _maxDelay - Stopwatch.GetElapsedTime(_burstStart);
            var due = remaining < _quietPeriod ? remaining : _quietPeriod;
            _timer.Change(due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _timer.Dispose();
    }

    private void Flush()
    {
        int total;
        lock (_lock)
        {
            if (_disposed || !_burstActive)
            {
                return;
            }

            total = (int)Math.Clamp(_pending, int.MinValue, int.MaxValue);
            _pending = 0;
            _burstActive = false;
        }

        if (total != 0)
        {
            _ = RunAsync(total);
        }
    }

    private async Task RunAsync(int total)
    {
        try
        {
            await _flush(total).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }
}
