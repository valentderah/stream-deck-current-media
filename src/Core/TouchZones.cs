namespace CurrentMedia;

internal static class TouchZones
{
    public const int StripWidth = 200;
    public const int StripHeight = 100;
    public const int CoverSize = 56;
    public const int TextWidth = 122;
    public const int TitleFontSize = 16;
    public const int ArtistFontSize = 13;
    public const int BarLeft = 54;
    public const int BarWidth = 92;
    public const int ProgressZoneTop = 66;

    public static bool IsProgressZone(int y) => y >= ProgressZoneTop;

    public static bool IsOnBar(int x) => x >= BarLeft && x <= BarLeft + BarWidth;

    public static double Fraction(int x) => Math.Clamp((x - BarLeft) / (double)BarWidth, 0, 1);

    public static double? SeekTarget(int[]? tapPosition, bool tapSeek, double duration)
    {
        if (!tapSeek || !double.IsFinite(duration) || duration <= 0)
        {
            return null;
        }

        if (tapPosition is not { Length: >= 2 } || !IsProgressZone(tapPosition[1]) || !IsOnBar(tapPosition[0]))
        {
            return null;
        }

        return Fraction(tapPosition[0]) * duration;
    }
}
