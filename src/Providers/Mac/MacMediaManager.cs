using System.Diagnostics;
using System.Text.Json;
using BarRaider.SdTools;
using CurrentMedia.Imaging;

namespace CurrentMedia.Mac;

public sealed class MacMediaManager : IMediaManager
{
    private const int MaxCrashRetries = 3;
    private const string DataPrefix = "DATA:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly object _processLock = new();
    private readonly SemaphoreSlim _stdinSemaphore = new(1, 1);

    private Process? _process;
    private bool _disposed;
    private bool _isInitialized;
    private int _crashRetryCount;

    public event EventHandler<MediaState>? MediaStateChanged;

    public async Task InitializeAsync()
    {
        if (_disposed || _isInitialized)
        {
            return;
        }

        if (!StartBridgeProcess())
        {
            return;
        }

        _isInitialized = true;
        await RequestUpdateAsync();
    }

    public Task RequestUpdateAsync()
    {
        // Bridge emits an initial snapshot on process start; no explicit refresh command needed.
        return Task.CompletedTask;
    }

    public Task PlayPauseAsync() => SendCommandAsync("play_pause");

    public Task NextAsync() => SendCommandAsync("next");

    public Task PreviousAsync() => SendCommandAsync("previous");

    public Task SeekForwardAsync() => SendCommandAsync("seek_forward");

    public Task SeekBackwardAsync() => SendCommandAsync("seek_backward");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        KillProcess();
        _stdinSemaphore.Dispose();
    }

    private string ResolveBridgePath() =>
        Path.Combine(AppContext.BaseDirectory, "media-bridge");

    private bool StartBridgeProcess()
    {
        lock (_processLock)
        {
            if (_disposed)
            {
                return false;
            }

            if (_process is { HasExited: false })
            {
                return true;
            }

            var bridgePath = ResolveBridgePath();
            if (!File.Exists(bridgePath))
            {
                Logger.Instance.LogMessage(
                    TracingLevel.ERROR,
                    $"media-bridge not found at {bridgePath}. macOS media support is unavailable.");
                return false;
            }

            try
            {
                KillProcessLocked();

                var baseDir = AppContext.BaseDirectory;
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = bridgePath,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = baseDir
                    }
                };

                _process.Start();
                _ = ReadStdoutLoopAsync(_process);
                _ = ReadStderrLoopAsync(_process);

                _crashRetryCount = 0;
                Logger.Instance.LogMessage(TracingLevel.INFO, "media-bridge process started.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(
                    TracingLevel.ERROR,
                    $"Failed to start media-bridge: {ex.Message}");
                return false;
            }
        }
    }

    private async Task ReadStdoutLoopAsync(Process process)
    {
        try
        {
            while (!_disposed)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                if (!line.StartsWith(DataPrefix, StringComparison.Ordinal))
                {
                    Logger.Instance.LogMessage(TracingLevel.TRACE, $"media-bridge stdout: {line}");
                    continue;
                }

                TryDeserializeAndNotify(line[DataPrefix.Length..]);
            }
        }
        catch (Exception ex) when (!_disposed)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"media-bridge stdout read error: {ex.Message}");
        }

        if (!_disposed)
        {
            await HandleProcessCrashAsync(process);
        }
    }

    private async Task ReadStderrLoopAsync(Process process)
    {
        try
        {
            while (!_disposed)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                Logger.Instance.LogMessage(TracingLevel.WARN, $"media-bridge stderr: {line}");
            }
        }
        catch (Exception ex) when (!_disposed)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"media-bridge stderr read error: {ex.Message}");
        }
    }

    private void TryDeserializeAndNotify(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<BridgeStateDto>(json, JsonOptions);
            if (dto == null)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, "media-bridge DATA line deserialized to null.");
                return;
            }

            var mediaState = dto.ToMediaState();
            ImagePipeline.PrepareCache(mediaState);
            MediaStateChanged?.Invoke(this, mediaState);
        }
        catch (JsonException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid media-bridge JSON: {ex.Message}");
        }
    }

    private async Task HandleProcessCrashAsync(Process deadProcess)
    {
        lock (_processLock)
        {
            if (_disposed || _process != deadProcess)
            {
                return;
            }

            _process = null;
        }

        Logger.Instance.LogMessage(TracingLevel.WARN, "media-bridge process exited unexpectedly.");

        _crashRetryCount++;
        if (_crashRetryCount > MaxCrashRetries)
        {
            Logger.Instance.LogMessage(
                TracingLevel.ERROR,
                $"media-bridge failed after {MaxCrashRetries} restart attempts.");
            NotifyInactive();
            return;
        }

        var delayMs = (int)Math.Pow(2, _crashRetryCount - 1) * 1000;
        Logger.Instance.LogMessage(
            TracingLevel.INFO,
            $"Restarting media-bridge in {delayMs}ms (attempt {_crashRetryCount}/{MaxCrashRetries}).");

        await Task.Delay(delayMs);

        if (_disposed)
        {
            return;
        }

        StartBridgeProcess();
    }

    private async Task SendCommandAsync(string command)
    {
        if (_disposed)
        {
            return;
        }

        await _stdinSemaphore.WaitAsync();
        try
        {
            Process? process = GetRunningProcess();
            if (process == null && !_isInitialized)
            {
                await InitializeAsync();
                process = GetRunningProcess();
            }

            if (process == null)
            {
                Logger.Instance.LogMessage(
                    TracingLevel.WARN,
                    $"Cannot send cmd:{command} — media-bridge is not running.");
                return;
            }

            await process.StandardInput.WriteLineAsync($"cmd:{command}");
            await process.StandardInput.FlushAsync();
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"Failed to send cmd:{command}: {ex.Message}");
        }
        finally
        {
            _stdinSemaphore.Release();
        }
    }

    private Process? GetRunningProcess()
    {
        lock (_processLock)
        {
            return _process is { HasExited: false } ? _process : null;
        }
    }

    private void NotifyInactive()
    {
        var state = new MediaState { IsActive = false };
        MediaStateChanged?.Invoke(this, state);
    }

    private void KillProcess()
    {
        lock (_processLock)
        {
            KillProcessLocked();
        }
    }

    private void KillProcessLocked()
    {
        if (_process == null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Error stopping media-bridge: {ex.Message}");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
