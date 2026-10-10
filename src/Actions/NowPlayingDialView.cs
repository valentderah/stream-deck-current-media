using BarRaider.SdTools;
using CurrentMedia.Imaging;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

internal sealed class NowPlayingDialView : IDisposable
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(1);

    private readonly Func<JObject, Task> _setFeedback;
    private readonly NowPlayingSettings _settings;
    private readonly TimeProvider _time;
    private readonly TimelineTracker _tracker = new();
    private readonly DialFeedback _feedback = new();
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private readonly object _stateLock = new();
    private MediaState _state = new();
    private string _coverKey = "";
    private int _marqueeOffset;
    private Timer? _progressTimer;
    private Timer? _marqueeTimer;
    private bool _started;
    private volatile bool _disposed;

    public NowPlayingDialView(Func<JObject, Task> setFeedback, NowPlayingSettings settings, TimeProvider time)
    {
        _setFeedback = setFeedback;
        _settings = settings;
        _time = time;
        _coverKey = CoverKey(_state);
    }

    public double Duration
    {
        get
        {
            lock (_stateLock)
            {
                return _tracker.Current.HasDuration ? _tracker.Current.Duration : 0;
            }
        }
    }

    public double Position
    {
        get
        {
            lock (_stateLock)
            {
                return PlaybackClock.PositionAt(_tracker.Current, _time.GetUtcNow());
            }
        }
    }

    public void Start()
    {
        if (_disposed || _started)
        {
            return;
        }

        _started = true;
        _progressTimer = new Timer(_ => _ = RenderAsync(), null, ProgressInterval, ProgressInterval);
        RestartMarqueeTimer();
    }

    public Task ShowAsync(MediaState state)
    {
        lock (_stateLock)
        {
            if (state.Title != _state.Title || state.Artist != _state.Artist)
            {
                _tracker.ReleaseHold();
            }

            _state = state;
            _coverKey = CoverKey(state);
        }

        return RenderAsync();
    }

    public Task OnTimelineAsync(MediaTimeline timeline)
    {
        lock (_stateLock)
        {
            _tracker.Accept(timeline, _time.GetUtcNow());
        }

        return RenderAsync();
    }

    public Task ShiftAsync(double offsetSeconds)
    {
        lock (_stateLock)
        {
            _tracker.Shift(offsetSeconds, _time.GetUtcNow());
        }

        return RenderAsync();
    }

    public Task JumpAsync(double position)
    {
        lock (_stateLock)
        {
            _tracker.Jump(position, _time.GetUtcNow());
        }

        return RenderAsync();
    }

    public Task ApplySettingsAsync()
    {
        lock (_stateLock)
        {
            _coverKey = CoverKey(_state);
        }

        if (_started)
        {
            RestartMarqueeTimer();
        }

        return RenderAsync();
    }

    public void Dispose()
    {
        _disposed = true;
        _progressTimer?.Dispose();
        _progressTimer = null;
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;
    }

    internal async Task RenderAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _renderGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            MediaState state;
            MediaTimeline timeline;
            string coverKey;
            lock (_stateLock)
            {
                state = _state;
                timeline = _tracker.Current;
                coverKey = _coverKey;
            }

            var frame = DialFrameBuilder.Build(
                state,
                timeline,
                _time.GetUtcNow(),
                _settings.TextDisplayMode,
                _settings.MarqueeSpeed,
                Volatile.Read(ref _marqueeOffset));
            var payload = _feedback.Next(
                frame,
                coverKey,
                () => ImagePipeline.RenderStripCover(HasMedia(state), _settings.IdleImage));
            if (payload == null)
            {
                return;
            }

            try
            {
                await _setFeedback(payload).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _feedback.Reset();
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error sending dial feedback: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _feedback.Reset();
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error rendering dial: {ex.Message}");
        }
        finally
        {
            _renderGate.Release();
        }
    }

    private void RestartMarqueeTimer()
    {
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;

        var interval = MarqueeHelper.IntervalMs(_settings.MarqueeSpeed);
        if (interval <= 0 || _disposed)
        {
            return;
        }

        _marqueeTimer = new Timer(_ =>
        {
            Interlocked.Increment(ref _marqueeOffset);
            _ = RenderAsync();
        }, null, interval, interval);
    }

    private string CoverKey(MediaState state) => HasMedia(state)
        ? "cover:" + Fnv1a.Hash32Hex(state.CoverArtBase64)
        : "idle:" + Fnv1a.Hash32Hex(_settings.IdleImage);

    private static bool HasMedia(MediaState state) => state.IsActive && state.HasMediaData;
}
