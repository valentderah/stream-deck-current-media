using System.Diagnostics;
using CurrentMedia.Imaging;

namespace CurrentMedia.Mac;

internal static class MacArtworkConverter
{
    private const int MaxCoverSize = 600;

    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, string> Cache = new();

    public static string NormalizeToDisplayBase64(string artworkBase64)
    {
        if (string.IsNullOrEmpty(artworkBase64))
        {
            return "";
        }

        lock (CacheLock)
        {
            if (Cache.TryGetValue(artworkBase64, out var cached))
            {
                return cached;
            }
        }

        using var decoded = ImageExtensions.DecodeFromBase64(artworkBase64);
        if (decoded != null)
        {
            Remember(artworkBase64, artworkBase64);
            return artworkBase64;
        }

        var converted = ConvertWithSips(artworkBase64);
        Remember(artworkBase64, converted);
        return converted;
    }

    private static void Remember(string source, string result)
    {
        lock (CacheLock)
        {
            if (Cache.Count > 8)
            {
                Cache.Clear();
            }

            Cache[source] = result;
        }
    }

    private static string ConvertWithSips(string artworkBase64)
    {
        string? inputPath = null;
        string? outputPath = null;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "ru.valentderah.current-media");
            Directory.CreateDirectory(tempDir);

            var id = Guid.NewGuid().ToString("N");
            inputPath = Path.Combine(tempDir, $"artwork-in-{id}.bin");
            outputPath = Path.Combine(tempDir, $"artwork-out-{id}.jpg");

            File.WriteAllBytes(inputPath, Convert.FromBase64String(artworkBase64));

            if (!RunSips("-s", "format", "jpeg", inputPath, "--out", outputPath))
            {
                return "";
            }

            if (!RunSips("-Z", MaxCoverSize.ToString(), outputPath))
            {
                return "";
            }

            if (!File.Exists(outputPath))
            {
                return "";
            }

            var jpegBytes = File.ReadAllBytes(outputPath);
            using var converted = ImageExtensions.DecodeFromBase64(Convert.ToBase64String(jpegBytes));
            return converted == null ? "" : Convert.ToBase64String(jpegBytes);
        }
        catch
        {
            return "";
        }
        finally
        {
            if (inputPath != null)
            {
                TryDelete(inputPath);
            }

            if (outputPath != null)
            {
                TryDelete(outputPath);
            }
        }
    }

    private static bool RunSips(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/usr/bin/sips",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return false;
        }

        process.WaitForExit(5000);
        return process.ExitCode == 0;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
