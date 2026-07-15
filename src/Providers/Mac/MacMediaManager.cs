using System.Diagnostics;
using System.Text.Json;
using BarRaider.SdTools;
using CurrentMedia.Imaging;

namespace CurrentMedia.Mac;

public sealed class MacMediaManager : IMediaManager
{
    private const int MaxCrashRetries = 3;
    private const int SeekStepSeconds = 10;
    private const int StreamDebounceMs = 250;

    private const int CommandTogglePlayPause = 2;
    private const int CommandNextTrack = 4;
    private const int CommandPreviousTrack = 5;

    private readonly object _processLock = new();
    private readonly SemaphoreSlim _commandSemaphore = new(1, 1);

    private Process? _streamProcess;
    private bool _disposed;
    private bool _isInitialized;
    private int _crashRetryCount;
    private double _lastElapsedTime;
    private string _adapterRoot = "";
    private string _frameworkPath = "";
    private string _perlScriptPath = "";

    public event EventHandler<MediaState>? MediaStateChanged;

    public async Task InitializeAsync()
    {
        if (_disposed || _isInitialized)
        {
            return;
        }

        if (!PrepareAdapterAssets() || !StartStreamProcess())
        {
            return;
        }

        _isInitialized = true;
        await RequestUpdateAsync();
    }

    public Task RequestUpdateAsync() => Task.CompletedTask;

    public Task PlayPauseAsync() => RunAdapterCommandAsync("send", CommandTogglePlayPause.ToString());

    public Task NextAsync() => RunAdapterCommandAsync("send", CommandNextTrack.ToString());

    public Task PreviousAsync() => RunAdapterCommandAsync("send", CommandPreviousTrack.ToString());

    public Task SeekForwardAsync() => SeekByAsync(SeekStepSeconds);

    public Task SeekBackwardAsync() => SeekByAsync(-SeekStepSeconds);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        KillProcess();
        _commandSemaphore.Dispose();
    }

    private bool PrepareAdapterAssets()
    {
        var macDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        var sourceFramework = Path.Combine(macDir, "MediaRemoteAdapter.framework");
        var sourcePerlScript = Path.Combine(macDir, "mediaremote-adapter.pl");

        if (!File.Exists(sourcePerlScript) || !Directory.Exists(sourceFramework))
        {
            Logger.Instance.LogMessage(
                TracingLevel.ERROR,
                $"mediaremote-adapter assets not found in {macDir}. macOS media support is unavailable.");
            return false;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "ru.valentderah.current-media", "mediaremote-adapter");
        Directory.CreateDirectory(tempDir);

        _adapterRoot = tempDir;
        _frameworkPath = Path.Combine(tempDir, "MediaRemoteAdapter.framework");
        _perlScriptPath = Path.Combine(tempDir, "mediaremote-adapter.pl");

        CopyIfNewer(sourcePerlScript, _perlScriptPath, isExecutable: true);
        CopyFrameworkIfNewer(sourceFramework, _frameworkPath);
        return true;
    }

    private static void CopyIfNewer(string source, string destination, bool isExecutable)
    {
        var sourceTime = File.GetLastWriteTimeUtc(source);
        if (File.Exists(destination) && File.GetLastWriteTimeUtc(destination) >= sourceTime)
        {
            return;
        }

        File.Copy(source, destination, overwrite: true);
        if (isExecutable)
        {
            File.SetUnixFileMode(
                destination,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static void CopyFrameworkIfNewer(string sourceFramework, string destinationFramework)
    {
        var sourceBinary = Path.Combine(sourceFramework, "MediaRemoteAdapter");
        if (!File.Exists(sourceBinary))
        {
            return;
        }

        var destinationBinary = Path.Combine(destinationFramework, "MediaRemoteAdapter");
        var sourceTime = File.GetLastWriteTimeUtc(sourceBinary);
        if (File.Exists(destinationBinary) && File.GetLastWriteTimeUtc(destinationBinary) >= sourceTime)
        {
            return;
        }

        Directory.CreateDirectory(destinationFramework);
        File.Copy(sourceBinary, destinationBinary, overwrite: true);
        File.SetUnixFileMode(
            destinationBinary,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private bool StartStreamProcess()
    {
        lock (_processLock)
        {
            if (_disposed)
            {
                return false;
            }

            if (_streamProcess is { HasExited: false })
            {
                return true;
            }

            try
            {
                KillProcessLocked();

                _streamProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/perl",
                        UseShellExecute = false,
                        RedirectStandardInput = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = _adapterRoot
                    }
                };

                _streamProcess.StartInfo.ArgumentList.Add(_perlScriptPath);
                _streamProcess.StartInfo.ArgumentList.Add(_frameworkPath);
                _streamProcess.StartInfo.ArgumentList.Add("stream");
                _streamProcess.StartInfo.ArgumentList.Add("--no-diff");
                _streamProcess.StartInfo.ArgumentList.Add($"--debounce={StreamDebounceMs}");

                _streamProcess.Start();
                _ = ReadStdoutLoopAsync(_streamProcess);
                _ = ReadStderrLoopAsync(_streamProcess);

                _crashRetryCount = 0;
                Logger.Instance.LogMessage(TracingLevel.INFO, "mediaremote-adapter stream started.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(
                    TracingLevel.ERROR,
                    $"Failed to start mediaremote-adapter stream: {ex.Message}");
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

                TryDeserializeAndNotify(line);
            }
        }
        catch (Exception ex) when (!_disposed)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"mediaremote-adapter stdout read error: {ex.Message}");
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

                Logger.Instance.LogMessage(TracingLevel.WARN, $"mediaremote-adapter stderr: {line}");
            }
        }
        catch (Exception ex) when (!_disposed)
        {
            Logger.Instance.LogMessage(TracingLevel.ERROR, $"mediaremote-adapter stderr read error: {ex.Message}");
        }
    }

    private void TryDeserializeAndNotify(string json)
    {
        try
        {
            var message = JsonSerializer.Deserialize(json, AdapterJsonContext.Default.AdapterStreamMessage);
            if (message?.Type != "data" || message.Payload == null)
            {
                return;
            }

            var payload = message.Payload;
            _lastElapsedTime = payload.ElapsedTimeNow ?? payload.ElapsedTime;

            var bundleId = !string.IsNullOrEmpty(payload.ParentApplicationBundleIdentifier)
                ? payload.ParentApplicationBundleIdentifier
                : payload.BundleIdentifier;
            var appIconBase64 = MacAppIconHelper.GetAppIconBase64(bundleId);

            var mediaState = payload.ToMediaState(appIconBase64);
            ImagePipeline.PrepareCache(mediaState);
            MediaStateChanged?.Invoke(this, mediaState);
        }
        catch (JsonException ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Invalid mediaremote-adapter JSON: {ex.Message}");
        }
    }

    private async Task HandleProcessCrashAsync(Process deadProcess)
    {
        lock (_processLock)
        {
            if (_disposed || _streamProcess != deadProcess)
            {
                return;
            }

            _streamProcess = null;
        }

        Logger.Instance.LogMessage(TracingLevel.WARN, "mediaremote-adapter stream exited unexpectedly.");

        _crashRetryCount++;
        if (_crashRetryCount > MaxCrashRetries)
        {
            Logger.Instance.LogMessage(
                TracingLevel.ERROR,
                $"mediaremote-adapter failed after {MaxCrashRetries} restart attempts.");
            NotifyInactive();
            return;
        }

        var delayMs = (int)Math.Pow(2, _crashRetryCount - 1) * 1000;
        Logger.Instance.LogMessage(
            TracingLevel.INFO,
            $"Restarting mediaremote-adapter in {delayMs}ms (attempt {_crashRetryCount}/{MaxCrashRetries}).");

        await Task.Delay(delayMs);

        if (_disposed)
        {
            return;
        }

        StartStreamProcess();
    }

    private Task RunAdapterCommandAsync(string function, string argument)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            await _commandSemaphore.WaitAsync();
            try
            {
                if (!_isInitialized && !PrepareAdapterAssets())
                {
                    return;
                }

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/perl",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        WorkingDirectory = _adapterRoot
                    }
                };

                process.StartInfo.ArgumentList.Add(_perlScriptPath);
                process.StartInfo.ArgumentList.Add(_frameworkPath);
                process.StartInfo.ArgumentList.Add(function);
                process.StartInfo.ArgumentList.Add(argument);

                process.Start();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Logger.Instance.LogMessage(
                        TracingLevel.WARN,
                        $"mediaremote-adapter {function} {argument} failed ({process.ExitCode}): {stderr.Trim()}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(
                    TracingLevel.ERROR,
                    $"Failed to run mediaremote-adapter {function} {argument}: {ex.Message}");
            }
            finally
            {
                _commandSemaphore.Release();
            }
        });
    }

    private Task SeekByAsync(int offsetSeconds)
    {
        var targetMicros = (long)Math.Max(0, (_lastElapsedTime + offsetSeconds) * 1_000_000);
        return RunAdapterCommandAsync("seek", targetMicros.ToString());
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
        if (_streamProcess == null)
        {
            return;
        }

        try
        {
            if (!_streamProcess.HasExited)
            {
                _streamProcess.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Error stopping mediaremote-adapter: {ex.Message}");
        }
        finally
        {
            _streamProcess.Dispose();
            _streamProcess = null;
        }
    }
}
