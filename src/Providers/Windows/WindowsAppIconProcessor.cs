using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using BarRaider.SdTools;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CurrentMedia.Windows;

static class WindowsAppIconProcessor
{
    private const int IconSize = 32;
    private const int MaxAttempts = 2;
    private static readonly ConcurrentDictionary<string, string> _iconCache = new();
    private static readonly ConcurrentDictionary<string, int> _failedAttempts = new();

    public static async Task<string> GetAppIconBase64Async(
        string appUserModelId,
        object? sourceAppInfo,
        TimeSpan timeout,
        CancellationToken token)
    {
        if (string.IsNullOrEmpty(appUserModelId))
        {
            return string.Empty;
        }

        if (_iconCache.TryGetValue(appUserModelId, out var cachedIcon))
        {
            return cachedIcon;
        }

        try
        {
            var icon = await ResolveAsync(appUserModelId, sourceAppInfo)
                .WaitAsync(timeout, token)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(icon))
            {
                _iconCache[appUserModelId] = icon;
                return icon;
            }
        }
        catch (TimeoutException)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"App icon lookup for {appUserModelId} timed out");
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"App icon lookup for {appUserModelId} failed: {ex.Message}");
        }

        RecordFailedAttempt(appUserModelId);
        return string.Empty;
    }

    private static void RecordFailedAttempt(string appUserModelId)
    {
        if (_failedAttempts.AddOrUpdate(appUserModelId, 1, (_, attempts) => attempts + 1) >= MaxAttempts)
        {
            // Stop probing the shell on every refresh for apps that have no reachable icon.
            _iconCache[appUserModelId] = string.Empty;
        }
    }

    private static async Task<string> ResolveAsync(string appUserModelId, dynamic? sourceAppInfo)
    {
        var fromSourceApp = await TryResolveFromSourceAppAsync(sourceAppInfo);
        if (!string.IsNullOrEmpty(fromSourceApp))
        {
            return fromSourceApp;
        }

        var packageFamilyName = appUserModelId.Split('!').FirstOrDefault();
        if (string.IsNullOrEmpty(packageFamilyName))
        {
            return string.Empty;
        }

        var packageManager = new global::Windows.Management.Deployment.PackageManager();
        var packages = packageManager.FindPackagesForUser(string.Empty, packageFamilyName);

        if (!packages.Any())
        {
            var exePath = FindExecutablePath(appUserModelId);
            return !string.IsNullOrEmpty(exePath) && File.Exists(exePath)
                ? await ConvertIconToBase64Async(exePath, IconSize)
                : string.Empty;
        }

        var package = packages.First();
        var appListEntries = await package.GetAppListEntriesAsync();
        var entry = appListEntries.FirstOrDefault(e => e.AppUserModelId == appUserModelId);

        var logo = entry?.DisplayInfo.GetLogo(new global::Windows.Foundation.Size(IconSize, IconSize));
        if (logo == null)
        {
            return string.Empty;
        }

        using var logoStream = await logo.OpenReadAsync();
        return await EncodeStreamToBase64Async(logoStream);
    }

    private static async Task<string> TryResolveFromSourceAppAsync(dynamic? sourceAppInfo)
    {
        if (sourceAppInfo == null)
        {
            return string.Empty;
        }

        try
        {
            var displayInfo = sourceAppInfo?.DisplayInfo;
            var logoStreamRef = displayInfo?.GetLogo(new global::Windows.Foundation.Size(IconSize, IconSize));
            if (logoStreamRef == null)
            {
                return string.Empty;
            }

            IRandomAccessStream? stream = await logoStreamRef.OpenReadAsync();
            using (stream)
            {
                return stream == null || stream.Size == 0
                    ? string.Empty
                    : await EncodeStreamToBase64Async(stream);
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"SourceAppInfo icon lookup failed: {ex.Message}");
            return string.Empty;
        }
    }

    private static string? FindExecutablePath(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
        }
        catch (Exception ex)
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"Failed to enumerate {processName}: {ex.Message}");
            return null;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var exePath = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        return exePath;
                    }
                }
                catch
                {
                    // Protected or exited process, try the next one.
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static async Task<string> EncodeStreamToBase64Async(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var transform = new BitmapTransform
        {
            ScaledWidth = IconSize,
            ScaledHeight = IconSize,
            InterpolationMode = BitmapInterpolationMode.Linear
        };
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        var pixels = pixelData.DetachPixelData();
        return await EncodeImageToBase64Async(pixels, IconSize);
    }

    private static async Task<string> EncodeImageToBase64Async(byte[] pixels, int size)
    {
        using var outputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Premultiplied,
            (uint)size,
            (uint)size,
            96.0,
            96.0,
            pixels
        );
        await encoder.FlushAsync();

        outputStream.Seek(0);
        var outputBuffer = new global::Windows.Storage.Streams.Buffer((uint)outputStream.Size);
        await outputStream.ReadAsync(outputBuffer, (uint)outputStream.Size, InputStreamOptions.None);

        return Convert.ToBase64String(outputBuffer.ToArray());
    }

    private static async Task<string> ConvertIconToBase64Async(string exePath, int size)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
            {
                return string.Empty;
            }

            using var bitmap = new Bitmap(icon.ToBitmap(), size, size);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var stride = bitmapData.Stride;

                var bgraBytes = new byte[stride * height];
                Marshal.Copy(bitmapData.Scan0, bgraBytes, 0, bgraBytes.Length);

                var rgbaBytes = new byte[width * height * 4];

                for (var y = 0; y < height; y++)
                {
                    var srcRowOffset = y * stride;
                    var dstRowOffset = y * width * 4;

                    for (var x = 0; x < width; x++)
                    {
                        var srcIndex = srcRowOffset + (x * 4);
                        var dstIndex = dstRowOffset + (x * 4);

                        rgbaBytes[dstIndex] = bgraBytes[srcIndex + 2];
                        rgbaBytes[dstIndex + 1] = bgraBytes[srcIndex + 1];
                        rgbaBytes[dstIndex + 2] = bgraBytes[srcIndex];
                        rgbaBytes[dstIndex + 3] = bgraBytes[srcIndex + 3];
                    }
                }

                return await EncodeImageToBase64Async(rgbaBytes, width);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
