using BarRaider.SdTools;
using CurrentMedia.Imaging;

namespace CurrentMedia.Actions;

internal sealed class NowPlayingKeyView : IDisposable
{
    private readonly Func<string, Task> _setImage;
    private readonly Func<string, Task> _setTitle;
    private readonly NowPlayingSettings _settings;
    private readonly SemaphoreSlim _titleGate = new(1, 1);
    private MediaState _state = new();
    private int _marqueeOffset;
    private Timer? _marqueeTimer;
    private volatile bool _disposed;

    public NowPlayingKeyView(
        Func<string, Task> setImage,
        Func<string, Task> setTitle,
        NowPlayingSettings settings)
    {
        _setImage = setImage;
        _setTitle = setTitle;
        _settings = settings;
    }

    public void Start() => RestartMarqueeTimer();

    public async Task ShowAsync(MediaState state)
    {
        Volatile.Write(ref _state, state);
        await UpdateImageAsync(state);
        await UpdateTitleAsync();
    }

    public Task ApplySettingsAsync()
    {
        if (_marqueeTimer != null || _settings.MarqueeSpeed > 0)
        {
            RestartMarqueeTimer();
        }

        return ShowAsync(Volatile.Read(ref _state));
    }

    public void Dispose()
    {
        _disposed = true;
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;
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
            _ = UpdateTitleAsync();
        }, null, interval, interval);
    }

    private async Task UpdateImageAsync(MediaState state)
    {
        try
        {
            if (!state.HasMediaData)
            {
                if (!string.IsNullOrEmpty(_settings.IdleImage))
                {
                    await _setImage(_settings.IdleImage);
                    return;
                }

                var imageSize = _settings.Position is ImagePosition.None or ImagePosition.NoImage
                    ? ImagePipeline.TargetSize
                    : ImagePipeline.PartSize;
                using var transparent = ImageExtensions.CreateTransparent(imageSize);
                await _setImage(ImageExtensions.ToPngDataUri(transparent));
                return;
            }

            var dataUri = ImagePipeline.RenderForPosition(
                state,
                _settings.Position,
                _settings.CropMode,
                _settings.OverlayDisplayMode,
                _settings.IdleImage);
            await _setImage(dataUri);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating image: {ex.Message}");
        }
    }

    private async Task UpdateTitleAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _titleGate.WaitAsync();
        try
        {
            var text = NowPlayingText.KeyTitle(
                Volatile.Read(ref _state),
                _settings.TextDisplayMode,
                _settings.MarqueeSpeed,
                Volatile.Read(ref _marqueeOffset));
            await _setTitle(text);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating title: {ex.Message}");
        }
        finally
        {
            _titleGate.Release();
        }
    }
}
