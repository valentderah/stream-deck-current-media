using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace MediaManager.Windows;

class MediaSessionManager
{
    private static readonly SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);
    private static GlobalSystemMediaTransportControlsSession? _currentSession;
    private static GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private static readonly HashSet<string> _subscribedSessions = new HashSet<string>();
    private static string? _lastTrackTitle;
    private static string? _lastTrackArtist;
    private static Timer? _updateDebounceTimer;
    private static readonly object _debounceLock = new object();

    public static async Task RunAsync()
    {
        var sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _sessionManager = sessionManager;
        sessionManager.CurrentSessionChanged += (s, e) => OnSessionChanged(s);
        sessionManager.SessionsChanged += OnSessionsChanged;
        OnSessionChanged(sessionManager);
        SubscribeToAllSessions(sessionManager);

        while (true)
        {
            try
            {
                var command = await Console.In.ReadLineAsync();
                
                if (command == null)
                {
                    break;
                }
                
                if (string.IsNullOrEmpty(command)) continue;

                switch (command.Trim().ToLower())
                {
                    case "toggle":
                        await TogglePlayPauseAsync();
                        break;
                    case "next":
                        await NextTrackAsync();
                        _ = Task.Run(async () => await WaitForTrackChangeAsync());
                        break;
                    case "previous":
                        await PreviousTrackAsync();
                        _ = Task.Run(async () => await WaitForTrackChangeAsync());
                        break;
                    case "update":
                        _ = UpdateCurrentMediaInfoAsync();
                        break;
                }
            }
            catch (IOException)
            {
                break;
            }
            catch
            {
                continue;
            }
        }
    }

    private static void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager manager, SessionsChangedEventArgs args)
    {
        SubscribeToAllSessions(manager);
        OnSessionChanged(manager);
    }

    private static void SubscribeToSession(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            var sessionId = session.SourceAppUserModelId;
            if (!_subscribedSessions.Contains(sessionId))
            {
                session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _subscribedSessions.Add(sessionId);
            }
        }
        catch
        {
        }
    }

    private static void UnsubscribeFromSession(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }
        catch
        {
        }
    }

    private static void SubscribeToAllSessions(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        try
        {
            var allSessions = manager.GetSessions();
            foreach (var session in allSessions)
            {
                SubscribeToSession(session);
            }
        }
        catch
        {
        }
    }

    private static void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        _sessionManager = manager;
        
        if (_currentSession != null)
        {
            UnsubscribeFromSession(_currentSession);
        }

        _currentSession = FindBestMediaSession(manager);

        if (_currentSession != null)
        {
            SubscribeToSession(_currentSession);
        }

        _ = UpdateCurrentMediaInfoAsync();
    }

    private static void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args)
    {
        DebouncedUpdate(100);
    }

    private static void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
    {
        DebouncedUpdate(100);
    }

    private static void DebouncedUpdate(int delayMs)
    {
        lock (_debounceLock)
        {
            _updateDebounceTimer?.Dispose();
            _updateDebounceTimer = new Timer(_ =>
            {
                _ = UpdateCurrentMediaInfoAsync();
            }, null, delayMs, Timeout.Infinite);
        }
    }

    private static async Task UpdateCurrentMediaInfoAsync()
    {
        await _updateSemaphore.WaitAsync();

        try
        {
            var mediaInfo = await GetCurrentMediaInfoAsync();
            var json = JsonSerializer.Serialize(mediaInfo, MediaInfoJsonContext.Default.MediaInfo);
            await Console.Out.WriteLineAsync(json);
        }
        catch
        {
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    static async Task<MediaInfo> GetCurrentMediaInfoAsync()
    {
        try
        {
            var sessionManager = _sessionManager ?? await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var activeSession = FindBestMediaSession(sessionManager);

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
            catch
            {
            }

            try
            {
                playbackInfo = activeSession.GetPlaybackInfo();
            }
            catch
            {
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
                catch
                {
                }
            }

            var title = mediaProperties?.Title ?? string.Empty;
            var artist = mediaProperties?.Artist ?? string.Empty;

            _lastTrackTitle = title;
            _lastTrackArtist = artist;

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

            if (mediaProperties != null && mediaProperties.Thumbnail != null)
            {
                try
                {
                    await ThumbnailProcessor.ProcessThumbnailAsync(mediaProperties.Thumbnail, info);
                }
                catch
                {
                }
            }

            return info;
        }
        catch
        {
            return new MediaInfo();
        }
    }

    private static GlobalSystemMediaTransportControlsSession? FindBestMediaSession(GlobalSystemMediaTransportControlsSessionManager manager)
    {
        try
        {
            GlobalSystemMediaTransportControlsSession? currentSession = null;
            try
            {
                currentSession = manager.GetCurrentSession();
            }
            catch
            {
            }

            if (currentSession != null)
            {
                try
                {
                    var playbackInfo = currentSession.GetPlaybackInfo();
                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        return currentSession;
                    }
                }
                catch
                {
                }
            }

            List<GlobalSystemMediaTransportControlsSession> allSessions;
            try
            {
                allSessions = manager.GetSessions().ToList();
            }
            catch
            {
                return currentSession;
            }

            var playingSessions = new List<GlobalSystemMediaTransportControlsSession>();
            foreach (var session in allSessions)
            {
                try
                {
                    var playbackInfo = session.GetPlaybackInfo();
                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        playingSessions.Add(session);
                    }
                }
                catch
                {
                }
            }

            if (playingSessions.Count > 0)
            {
                if (currentSession != null && playingSessions.Contains(currentSession))
                {
                    return currentSession;
                }
                return playingSessions.FirstOrDefault();
            }

            var pausedSessions = new List<GlobalSystemMediaTransportControlsSession>();
            foreach (var session in allSessions)
            {
                try
                {
                    var playbackInfo = session.GetPlaybackInfo();
                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                    {
                        pausedSessions.Add(session);
                    }
                }
                catch
                {
                }
            }

            if (pausedSessions.Count > 0 && currentSession != null && pausedSessions.Contains(currentSession))
            {
                return currentSession;
            }

            if (currentSession != null)
            {
                return currentSession;
            }

            return allSessions.FirstOrDefault();
        }
        catch
        {
            try
            {
                return manager.GetCurrentSession();
            }
            catch
            {
                return null;
            }
        }
    }

    static async Task<GlobalSystemMediaTransportControlsSession?> GetActiveSessionAsync()
    {
        var sessionManager = _sessionManager ?? await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        return FindBestMediaSession(sessionManager);
    }

    static async Task TogglePlayPauseAsync()
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession != null)
            {
                await activeSession.TryTogglePlayPauseAsync();
            }
        }
        catch
        {
        }
    }

    static async Task NextTrackAsync()
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession != null)
            {
                await activeSession.TrySkipNextAsync();
            }
        }
        catch
        {
        }
    }

    static async Task PreviousTrackAsync()
    {
        try
        {
            var activeSession = await GetActiveSessionAsync();
            if (activeSession != null)
            {
                await activeSession.TrySkipPreviousAsync();
            }
        }
        catch
        {
        }
    }

    private static async Task WaitForTrackChangeAsync()
    {
        var lastTitle = _lastTrackTitle ?? string.Empty;
        var lastArtist = _lastTrackArtist ?? string.Empty;
        var attempts = 0;
        var maxAttempts = 10;
        var delayMs = 200;

        while (attempts < maxAttempts)
        {
            await Task.Delay(delayMs);

            var session = await GetActiveSessionAsync();
            if (session != null)
            {
                try
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    if (props != null)
                    {
                        var currentTitle = props.Title ?? string.Empty;
                        var currentArtist = props.Artist ?? string.Empty;

                        if ((!string.IsNullOrEmpty(currentTitle) || !string.IsNullOrEmpty(currentArtist)) &&
                            (currentTitle != lastTitle || currentArtist != lastArtist))
                        {
                            _lastTrackTitle = currentTitle;
                            _lastTrackArtist = currentArtist;
                            await UpdateCurrentMediaInfoAsync();
                            return;
                        }
                    }
                }
                catch
                {
                }
            }
            attempts++;
        }

        await UpdateCurrentMediaInfoAsync();
    }
}
