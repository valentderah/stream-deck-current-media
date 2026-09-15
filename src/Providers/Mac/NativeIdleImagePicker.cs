using System.Diagnostics;

namespace CurrentMedia.Imaging;

static class NativeIdleImagePicker
{
    public static string? Pick()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                ArgumentList =
                {
                    "-e",
                    "POSIX path of (choose file of type {\"public.png\",\"public.jpeg\",\"org.webmproject.webp\"} with prompt \"Idle Background\")"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(120_000);
            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
