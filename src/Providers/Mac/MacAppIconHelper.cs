using System.Diagnostics;

namespace CurrentMedia.Mac;

internal static class MacAppIconHelper
{
    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static readonly object CacheLock = new();

    public static string GetAppIconBase64(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            return "";
        }

        lock (CacheLock)
        {
            if (Cache.TryGetValue(bundleId, out var cached))
            {
                return cached;
            }
        }

        var iconBase64 = TryReadIconBase64(bundleId);
        lock (CacheLock)
        {
            Cache[bundleId] = iconBase64;
        }

        return iconBase64;
    }

    private static string TryReadIconBase64(string bundleId)
    {
        try
        {
            var appPath = ResolveAppPath(bundleId);
            if (string.IsNullOrEmpty(appPath))
            {
                return "";
            }

            var iconPath = Path.Combine(appPath, "Contents", "Resources", "AppIcon.icns");
            if (!File.Exists(iconPath))
            {
                var resourcesDir = Path.Combine(appPath, "Contents", "Resources");
                if (!Directory.Exists(resourcesDir))
                {
                    return "";
                }

                iconPath = Directory
                    .EnumerateFiles(resourcesDir, "*.icns", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault() ?? "";
                if (string.IsNullOrEmpty(iconPath))
                {
                    return "";
                }
            }

            var tempPng = Path.Combine(
                Path.GetTempPath(),
                $"ru.valentderah.current-media-icon-{bundleId.GetHashCode():X}.png");

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/sips",
                ArgumentList = { "-s", "format", "png", iconPath, "--out", tempPng, "-z", "64", "64" },
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(3000);
            if (process is not { ExitCode: 0 } || !File.Exists(tempPng))
            {
                return "";
            }

            var bytes = File.ReadAllBytes(tempPng);
            File.Delete(tempPng);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return "";
        }
    }

    private static string ResolveAppPath(string bundleId)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/bin/mdfind",
            ArgumentList = { $"kMDItemCFBundleIdentifier == '{bundleId}'" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process == null)
        {
            return "";
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(3000);
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(path => Directory.Exists(path)) ?? "";
    }
}
