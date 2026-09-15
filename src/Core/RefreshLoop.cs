using System.Diagnostics;

namespace CurrentMedia;

internal sealed class RefreshLoopOptions
{
    public TimeSpan QuietPeriod { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMilliseconds(750);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}

internal sealed class RefreshLoop : IDisposable
{
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly RefreshLoopOptions _options;
    private readonly Action<Exception>? _onError;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;
    private int _started;
    private volatile bool _disposed;

    public RefreshLoop(
        Func<CancellationToken, Task> refresh,
        RefreshLoopOptions? options = null,
        Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        _refresh = refresh;
        _options = options ?? new RefreshLoopOptions();
        _onError = onError;

        if (_options.QuietPeriod <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "QuietPeriod must be positive");
        }

        if (_options.MaxDelay < _options.QuietPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxDelay must be at least QuietPeriod");
        }

        if (_options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "PollInterval must be positive");
        }
    }

    public void Start()
    {
        if (_disposed || Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        Request();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Request()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_signal.CurrentCount == 0)
            {
                _signal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Stop(TimeSpan timeout)
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        var loop = _loopTask;
        if (loop == null)
        {
            return;
        }

        try
        {
            loop.Wait(timeout);
        }
        catch (AggregateException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop(TimeSpan.FromSeconds(2));

        try
        {
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _signal.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            bool signaled;
            try
            {
                signaled = await _signal.WaitAsync(_options.PollInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (signaled && !await SettleAsync(token).ConfigureAwait(false))
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _refresh(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
        }
    }

    private async Task<bool> SettleAsync(CancellationToken token)
    {
        var start = Stopwatch.GetTimestamp();

        while (true)
        {
            var remaining = _options.MaxDelay - Stopwatch.GetElapsedTime(start);
            if (remaining <= TimeSpan.Zero)
            {
                return true;
            }

            try
            {
                await Task.Delay(remaining < _options.QuietPeriod ? remaining : _options.QuietPeriod, token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            if (!TryConsumeSignal())
            {
                return true;
            }
        }
    }

    private bool TryConsumeSignal()
    {
        try
        {
            return _signal.Wait(0);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
