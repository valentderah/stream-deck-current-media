using System.Runtime.InteropServices.WindowsRuntime;
using BarRaider.SdTools;
using CurrentMedia.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace CurrentMedia.Windows;

public sealed class WindowsMediaManager : IMediaManager
{
    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts = new();
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _subscribedSessions = new();
    private GlobalSystemMediaTransportControlsSession? _lastActiveSession;
    private Timer? _updateDebounceTimer;
    private readonly object _debounceLock = new();
    private bool _isInitialized;
    private bool _disposed;

    public event EventHandler<MediaState>? MediaStateChanged;

    public async Task InitializeAsync()
    {
        if (_disposed || _isInitialized)
        {
            return;
        }

        try
        {
            var request = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
            var wait = await AsyncWait.For(request, MediaClientTimeouts.SessionManagerRequest, _shutdownCts.Token)
                .ConfigureAwait(false);
            if (_disposed)
            {
                return;
            }

            switch (wait.Kind)
            {
                case WaitResultKind.Completed:
                    break;
                case WaitResultKind.TimedOut:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, "SMTC RequestAsync timed out");
                    return;
                case WaitResultKind.Canceled:
                    return;
                default:
                {
                    WaitResultKind unexpected = wait.Kind;
                    throw new InvalidOperationException($"Unknown wait kind: {unexpected}");
                }
            }

            if (wait.Value is null)
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            _sessionManager = wait.Value;
            _sessionManager.CurrentSessionChanged += HandleCurrentSessionChanged;
            _sessionManager.SessionsChanged += HandleSessionsChanged;
            SubscribeToAllSessions(_sessionManager);
            if (_disposed)
            {
                TearDownSubscriptions();
                return;
            }

            _isInitialized = true;
            await UpdateAndNotifyAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to initialize WindowsMediaManager: {ex.Message}");
        }
    }

    private async Task<T?> AwaitSessionOpAsync<T>(Task<T> task, string operation)
    {
        var wait = await AsyncWait.For(task, MediaClientTimeouts.SessionOperation, _shutdownCts.Token)
            .ConfigureAwait(false);
        switch (wait.Kind)
        {
            case WaitResultKind.Completed:
                return _disposed ? default : wait.Value;
            case WaitResultKind.TimedOut:
                Logger.Instance.LogMessage(TracingLevel.WARN, $"{operation} timed out");
                return default;
            case WaitResultKind.Canceled:
                return default;
            default:
            {
                WaitResultKind unexpected = wait.Kind;
                throw new InvalidOperationException($"Unknown wait kind: {unexpected}");
            }
        }
    }

    private void HandleCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        OnSessionChanged();
    }

    private void HandleSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args)
    {
        OnSessionsChanged();
    }

    public async Task RequestUpdateAsync()
    {
        await UpdateAndNotifyAsync();
    }

    public async Task PlayPauseAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession == null)
            {
                return;
            }

            await AwaitSessionOpAsync(activeSession.TryTogglePlayPauseAsync().AsTask(), "TryTogglePlayPauseAsync");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error toggling play/pause: {ex.Message}");
        }
    }

    public async Task NextAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession == null)
            {
                return;
            }

            await AwaitSessionOpAsync(activeSession.TrySkipNextAsync().AsTask(), "TrySkipNextAsync");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error skipping next: {ex.Message}");
        }
    }

    public async Task PreviousAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession == null)
            {
                return;
            }

            await AwaitSessionOpAsync(activeSession.TrySkipPreviousAsync().AsTask(), "TrySkipPreviousAsync");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error skipping previous: {ex.Message}");
        }
    }

    public Task SeekByAsync(int offsetSeconds) => SeekAsync(TimeSpan.FromSeconds(offsetSeconds));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _shutdownCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        lock (_debounceLock)
        {
            _updateDebounceTimer?.Dispose();
            _updateDebounceTimer = null;
        }

        TearDownSubscriptions();

        try
        {
            _updateSemaphore.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            _shutdownCts.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void TearDownSubscriptions()
    {
        var manager = _sessionManager;
        if (manager != null)
        {
            try
            {
                manager.CurrentSessionChanged -= HandleCurrentSessionChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error unsubscribing CurrentSessionChanged: {ex.Message}");
            }

            try
            {
                manager.SessionsChanged -= HandleSessionsChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error unsubscribing SessionsChanged: {ex.Message}");
            }
        }

        foreach (var session in _subscribedSessions.Values)
        {
            try
            {
                session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error unsubscribing session: {ex.Message}");
            }
        }

        _subscribedSessions.Clear();
        _sessionManager = null;
    }

    private void OnSessionsChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (_sessionManager != null)
        {
            SubscribeToAllSessions(_sessionManager);
        }
    }

    private void SubscribeToAllSessions(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        try
        {
            var allSessions = manager.GetSessions();
            var currentSessionIds = new HashSet<string>();

            foreach (var session in allSessions)
            {
                try
                {
                    var sessionId = session.SourceAppUserModelId;
                    currentSessionIds.Add(sessionId);

                    if (_subscribedSessions.TryGetValue(sessionId, out var oldSession))
                    {
                        oldSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                        oldSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    }

                    session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _subscribedSessions[sessionId] = session;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to subscribe to session: {ex.Message}");
                }
            }

            var removedIds = _subscribedSessions.Keys.Where(id => !currentSessionIds.Contains(id)).ToList();
            foreach (var id in removedIds)
            {
                if (_subscribedSessions.TryGetValue(id, out var oldSession))
                {
                    oldSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    oldSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }

                _subscribedSessions.Remove(id);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to subscribe to all sessions: {ex.Message}");
        }
    }

    private void OnSessionChanged()
    {
        DebouncedUpdate(250);
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args)
    {
        DebouncedUpdate(250);
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
    {
        DebouncedUpdate(250);
    }

    private void DebouncedUpdate(int delayMs)
    {
        if (_disposed)
        {
            return;
        }

        lock (_debounceLock)
        {
            _updateDebounceTimer?.Dispose();
            _updateDebounceTimer = new Timer(_ =>
            {
                _ = UpdateAndNotifyAsync();
            }, null, delayMs, Timeout.Infinite);
        }
    }

    private async Task UpdateAndNotifyAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await _updateSemaphore.WaitAsync(_shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_disposed)
            {
                return;
            }

            var mediaState = await GetCurrentMediaStateAsync();
            ImagePipeline.PrepareCache(mediaState);
            MediaStateChanged?.Invoke(this, mediaState);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating media state: {ex.Message}");
        }
        finally
        {
            try
            {
                _updateSemaphore.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private async Task<MediaState> GetCurrentMediaStateAsync()
    {
        try
        {
            if (_sessionManager == null)
            {
                return InactiveState();
            }

            var activeSession = FindBestMediaSession(_sessionManager);
            if (activeSession == null)
            {
                return InactiveState();
            }

            GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProperties = null;
            GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo = null;

            try
            {
                mediaProperties = await AwaitSessionOpAsync(
                    activeSession.TryGetMediaPropertiesAsync().AsTask(),
                    "TryGetMediaPropertiesAsync");
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error getting media properties: {ex.Message}");
            }

            try
            {
                playbackInfo = activeSession.GetPlaybackInfo();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error getting playback info: {ex.Message}");
            }

            if (playbackInfo == null)
            {
                return InactiveState();
            }

            var artists = new List<string>();
            if (mediaProperties != null && !string.IsNullOrEmpty(mediaProperties.Artist))
            {
                try
                {
                    var artistParts = mediaProperties.Artist.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    artists.AddRange(artistParts.Select(a => a.Trim()).Where(a => !string.IsNullOrEmpty(a)));
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Error parsing artists: {ex.Message}");
                }
            }

            var title = mediaProperties?.Title ?? string.Empty;
            var artist = mediaProperties?.Artist ?? string.Empty;

            var hasMediaData = !string.IsNullOrEmpty(title)
                || !string.IsNullOrEmpty(artist)
                || artists.Count > 0;

            if (!hasMediaData)
            {
                return InactiveState();
            }

            var state = new MediaState
            {
                Title = title,
                Artist = artist,
                Artists = artists,
                AlbumArtist = mediaProperties?.AlbumArtist ?? string.Empty,
                AlbumTitle = mediaProperties?.AlbumTitle ?? string.Empty,
                Status = playbackInfo.PlaybackStatus switch
                {
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => "Playing",
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => "Paused",
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => "Stopped",
                    _ => "Stopped"
                },
                IsActive = true
            };

            if (state.Status == "Playing")
            {
                _lastActiveSession = activeSession;
            }

            try
            {
                var timelineProperties = activeSession.GetTimelineProperties();
                if (timelineProperties != null)
                {
                    state.Position = GetEffectivePlaybackPosition(timelineProperties, playbackInfo.PlaybackStatus).TotalSeconds;
                    var duration = timelineProperties.EndTime - timelineProperties.StartTime;
                    if (duration > TimeSpan.Zero)
                    {
                        state.Duration = duration.TotalSeconds;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error getting timeline properties: {ex.Message}");
            }

            if (mediaProperties?.Thumbnail != null)
            {
                try
                {
                    state.CoverArtBase64 = await GetThumbnailBase64Async(mediaProperties.Thumbnail);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Error reading thumbnail: {ex.Message}");
                }
            }

            try
            {
                var appUserModelId = activeSession.SourceAppUserModelId;
                if (!string.IsNullOrEmpty(appUserModelId))
                {
                    dynamic? sourceAppInfo = null;
                    try
                    {
                        var sourceAppInfoProperty = activeSession.GetType().GetProperty("SourceAppInfo");
                        if (sourceAppInfoProperty != null)
                        {
                            sourceAppInfo = sourceAppInfoProperty.GetValue(activeSession);
                        }
                    }
                    catch
                    {
                        // Property doesn't exist or is inaccessible
                    }

                    state.AppIconBase64 = await WindowsAppIconProcessor.GetAppIconBase64Async(appUserModelId, sourceAppInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error getting app icon: {ex.Message}");
            }

            return state;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error in GetCurrentMediaStateAsync: {ex.Message}");
            return InactiveState();
        }
    }

    private static MediaState InactiveState() => new() { IsActive = false };

    private async Task<string> GetThumbnailBase64Async(IRandomAccessStreamReference thumbnail)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 250;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (_disposed)
            {
                return string.Empty;
            }

            try
            {
                using var stream = await AwaitSessionOpAsync(
                    thumbnail.OpenReadAsync().AsTask(),
                    "thumbnail OpenReadAsync");
                if (stream is null)
                {
                    if (_disposed || attempt >= maxRetries)
                    {
                        return string.Empty;
                    }

                    await Task.Delay(retryDelayMs);
                    continue;
                }

                if (stream.Size == 0)
                {
                    return string.Empty;
                }

                stream.Seek(0);
                var buffer = new global::Windows.Storage.Streams.Buffer((uint)stream.Size);
                var read = await AwaitSessionOpAsync(
                    stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None).AsTask(),
                    "thumbnail ReadAsync");
                if (read is null)
                {
                    if (_disposed || attempt >= maxRetries)
                    {
                        return string.Empty;
                    }

                    await Task.Delay(retryDelayMs);
                    continue;
                }

                return Convert.ToBase64String(buffer.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Thumbnail read attempt {attempt}/{maxRetries} failed: {ex.Message}");
                if (_disposed)
                {
                    return string.Empty;
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelayMs);
                }
            }
        }

        return string.Empty;
    }

    private GlobalSystemMediaTransportControlsSession? FindBestMediaSession(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        try
        {
            var allSessions = manager.GetSessions();
            GlobalSystemMediaTransportControlsSession? pausedLastActive = null;
            GlobalSystemMediaTransportControlsSession? pausedCurrent = null;
            GlobalSystemMediaTransportControlsSession? anyPaused = null;

            var currentSystemSession = manager.GetCurrentSession();

            foreach (var session in allSessions)
            {
                try
                {
                    var playbackInfo = session.GetPlaybackInfo();
                    if (playbackInfo == null)
                    {
                        continue;
                    }

                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        return session;
                    }

                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                    {
                        if (_lastActiveSession != null && session.SourceAppUserModelId == _lastActiveSession.SourceAppUserModelId)
                        {
                            pausedLastActive = session;
                        }

                        if (currentSystemSession != null && session.SourceAppUserModelId == currentSystemSession.SourceAppUserModelId)
                        {
                            pausedCurrent = session;
                        }

                        anyPaused ??= session;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Error finding best session: {ex.Message}");
                }
            }

            return pausedCurrent
                ?? pausedLastActive
                ?? anyPaused
                ?? allSessions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Critical error in FindBestMediaSession: {ex.Message}");
            return null;
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> GetActiveSessionAsync()
    {
        if (_disposed)
        {
            return null;
        }

        if (_sessionManager == null)
        {
            await InitializeAsync();
        }

        if (_disposed)
        {
            return null;
        }

        return _sessionManager != null ? FindBestMediaSession(_sessionManager) : null;
    }

    private async Task SeekAsync(TimeSpan offset)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession == null)
            {
                return;
            }

            var playbackInfo = activeSession.GetPlaybackInfo();
            if (playbackInfo == null || !playbackInfo.Controls.IsPlaybackPositionEnabled)
            {
                return;
            }

            var timelineProperties = activeSession.GetTimelineProperties();
            if (timelineProperties == null)
            {
                return;
            }

            var currentPosition = GetEffectivePlaybackPosition(timelineProperties, playbackInfo.PlaybackStatus);
            var newPosition = currentPosition + offset;

            if (newPosition < timelineProperties.StartTime)
            {
                newPosition = timelineProperties.StartTime;
            }

            if (timelineProperties.EndTime > TimeSpan.Zero && newPosition > timelineProperties.EndTime)
            {
                newPosition = timelineProperties.EndTime;
            }

            await AwaitSessionOpAsync(
                activeSession.TryChangePlaybackPositionAsync(newPosition.Ticks).AsTask(),
                "TryChangePlaybackPositionAsync");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error seeking: {ex.Message}");
        }
    }

    private static TimeSpan GetEffectivePlaybackPosition(
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
    {
        var position = timeline.Position;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            position += DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
        }

        return position;
    }
}
