namespace CurrentMedia.Imaging;

static class MarqueeHelper
{
    private const int VisibleChars = 10;

    public static string GetMarqueeText(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= VisibleChars)
        {
            return text;
        }

        var padded = text + "   " + text;
        var off = offset % (text.Length + 3);
        return padded.Substring(off, Math.Min(VisibleChars, padded.Length - off));
    }
}
