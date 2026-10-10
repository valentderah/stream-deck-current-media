namespace CurrentMedia.Imaging;

static class MarqueeHelper
{
    private const int VisibleChars = 10;
    private const double MaxIntervalMs = 1500.0;
    private const double MinIntervalMs = 500.0;

    public static int IntervalMs(int speed)
    {
        if (speed <= 0)
        {
            return 0;
        }

        var clamped = Math.Clamp(speed, 1, 100);
        return (int)Math.Round(MaxIntervalMs * Math.Pow(MinIntervalMs / MaxIntervalMs, clamped / 100.0));
    }

    public static string GetMarqueeText(string text, int offset, int visibleChars = VisibleChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= visibleChars)
        {
            return text;
        }

        var padded = text + "   " + text;
        var off = offset % (text.Length + 3);
        return TextFit.GuardEdges(padded.Substring(off, Math.Min(visibleChars, padded.Length - off)));
    }
}
