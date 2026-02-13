using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using BarRaider.SdTools;
using CurrentMedia.Imaging;

namespace CurrentMedia;

public class MediaSessionManager
{
    private static readonly Lazy<MediaSessionManager> _instance = new(() => new MediaSessionManager());
    public static MediaSessionManager Instance => _instance.Value;

    private readonly SemaphoreSlim _updateSemaphore = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _subscribedSessions = new();
    private GlobalSystemMediaTransportControlsSession? _lastActiveSession;
    private Timer? _updateDebounceTimer;
    private readonly object _debounceLock = new();
    private bool _isInitialized;

    public event EventHandler<MediaInfo>? MediaInfoChanged;

    private MediaSessionManager() { }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _sessionManager.CurrentSessionChanged += (s, e) => OnSessionChanged();
            _sessionManager.SessionsChanged += (s, e) => OnSessionsChanged();
            SubscribeToAllSessions(_sessionManager);
            _isInitialized = true;

            await UpdateAndNotifyAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to initialize MediaSessionManager: {ex.Message}");
        }
    }

    private void OnSessionsChanged()
    {
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

                    // Unsubscribe from old session object if exists
                    if (_subscribedSessions.TryGetValue(sessionId, out var oldSession))
                    {
                        oldSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                        oldSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                    }

                    // Subscribe to current session object and keep a strong reference
                    session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _subscribedSessions[sessionId] = session;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to subscribe to session: {ex.Message}");
                }
            }

            // Clean up sessions that no longer exist
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
        lock (_debounceLock)
        {
            _updateDebounceTimer?.Dispose();
            _updateDebounceTimer = new Timer(_ =>
            {
                _ = UpdateAndNotifyAsync();
            }, null, delayMs, Timeout.Infinite);
        }
    }

    public async Task RequestUpdateAsync()
    {
        await UpdateAndNotifyAsync();
    }

    private async Task UpdateAndNotifyAsync()
    {
        await _updateSemaphore.WaitAsync();

        try
        {
            var mediaInfo = await GetCurrentMediaInfoAsync();
            MediaInfoChanged?.Invoke(this, mediaInfo);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error updating media info: {ex.Message}");
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    private async Task<MediaInfo> GetCurrentMediaInfoAsync()
    {
        try
        {
            if (_sessionManager == null)
            {
                return new MediaInfo();
            }

            var activeSession = FindBestMediaSession(_sessionManager);

            if (activeSession == null)
            {
                return new MediaInfo();
            }

            GlobalSystemMediaTransportControlsSessionMediaProperties? mediaProperties = null;
            GlobalSystemMediaTransportControlsSessionPlaybackInfo? playbackInfo = null;

            try
            {
                mediaProperties = await activeSession.TryGetMediaPropertiesAsync();
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
                return new MediaInfo();
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

            var info = new MediaInfo
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
                }
            };

            if (info.Status == "Playing")
            {
                _lastActiveSession = activeSession;
            }

            if (mediaProperties?.Thumbnail != null)
            {
                try
                {
                    await ThumbnailProcessor.ProcessThumbnailAsync(mediaProperties.Thumbnail, info);
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"Error processing thumbnail: {ex.Message}");
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

                    info.AppIconBase64 = await AppIconProcessor.GetAppIconBase64Async(appUserModelId, sourceAppInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error getting app icon: {ex.Message}");
            }

            return info;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error in GetCurrentMediaInfoAsync: {ex.Message}");
            return new MediaInfo();
        }
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
                    if (playbackInfo == null) continue;

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
        if (_sessionManager == null)
        {
            await InitializeAsync();
        }
        return _sessionManager != null ? FindBestMediaSession(_sessionManager) : null;
    }

    public async Task TogglePlayPauseAsync()
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession != null)
            {
                await activeSession.TryTogglePlayPauseAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error toggling play/pause: {ex.Message}");
        }
    }

    public async Task NextTrackAsync()
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession != null)
            {
                await activeSession.TrySkipNextAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error skipping next: {ex.Message}");
        }
    }

    public async Task PreviousTrackAsync()
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession != null)
            {
                await activeSession.TrySkipPreviousAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error skipping previous: {ex.Message}");
        }
    }

    public async Task SeekForwardAsync()
    {
        await SeekAsync(TimeSpan.FromSeconds(10));
    }

    public async Task SeekBackwardAsync()
    {
        await SeekAsync(TimeSpan.FromSeconds(-10));
    }

    private async Task SeekAsync(TimeSpan offset)
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession == null) return;

            var playbackInfo = activeSession.GetPlaybackInfo();
            if (playbackInfo == null || !playbackInfo.Controls.IsPlaybackPositionEnabled)
            {
                return;
            }

            var timelineProperties = activeSession.GetTimelineProperties();
            if (timelineProperties == null) return;

            var newPosition = timelineProperties.Position + offset;

            if (newPosition < timelineProperties.StartTime)
            {
                newPosition = timelineProperties.StartTime;
            }

            if (timelineProperties.EndTime > TimeSpan.Zero && newPosition > timelineProperties.EndTime)
            {
                newPosition = timelineProperties.EndTime;
            }

            await activeSession.TryChangePlaybackPositionAsync(newPosition.Ticks);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error seeking: {ex.Message}");
        }
    }
}
