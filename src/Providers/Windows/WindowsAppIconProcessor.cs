using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CurrentMedia.Windows;

static class WindowsAppIconProcessor
{
    private const int IconSize = 32;
    private static readonly ConcurrentDictionary<string, string> _iconCache = new();

    public static async Task<string> GetAppIconBase64Async(string appUserModelId, dynamic? sourceAppInfo)
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
            if (sourceAppInfo != null)
            {
                try
                {
                    var displayInfo = sourceAppInfo?.DisplayInfo;
                    if (displayInfo != null)
                    {
                        var logoStreamRef = displayInfo.GetLogo(new global::Windows.Foundation.Size(IconSize, IconSize));
                        if (logoStreamRef != null)
                        {
                            using var stream = await logoStreamRef.OpenReadAsync();
                            if (stream != null && stream.Size > 0)
                            {
                                var result = await EncodeStreamToBase64Async(stream);
                                if (!string.IsNullOrEmpty(result))
                                {
                                    _iconCache.TryAdd(appUserModelId, result);
                                    return result;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }

            var packageManager = new global::Windows.Management.Deployment.PackageManager();
            var packageFamilyName = appUserModelId.Split('!').FirstOrDefault();

            if (string.IsNullOrEmpty(packageFamilyName))
            {
                return string.Empty;
            }

            var packages = packageManager.FindPackagesForUser(string.Empty, packageFamilyName);

            if (!packages.Any())
            {
                try
                {
                    var exePath = FindExecutablePath(appUserModelId);
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        try
                        {
                            var result = await ConvertIconToBase64Async(exePath, IconSize);
                            if (!string.IsNullOrEmpty(result))
                            {
                                _iconCache.TryAdd(appUserModelId, result);
                                return result;
                            }
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
                catch
                {
                    // ignored
                }

                return string.Empty;
            }

            var package = packages.First();
            var appListEntries = await package.GetAppListEntriesAsync();
            var entry = appListEntries.FirstOrDefault(e => e.AppUserModelId == appUserModelId);

            if (entry == null)
            {
                return string.Empty;
            }

            var logo = entry.DisplayInfo.GetLogo(new global::Windows.Foundation.Size(IconSize, IconSize));
            if (logo != null)
            {
                using var stream = await logo.OpenReadAsync();
                var result = await EncodeStreamToBase64Async(stream);
                if (!string.IsNullOrEmpty(result))
                {
                    _iconCache.TryAdd(appUserModelId, result);
                    return result;
                }
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    private static string? FindExecutablePath(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
            if (processes.Length > 0)
            {
                var process = processes[0];
                try
                {
                    var exePath = process.MainModule?.FileName;
                    process.Dispose();
                    return exePath;
                }
                catch
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // ignored
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
