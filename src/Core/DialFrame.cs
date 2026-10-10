namespace CurrentMedia;

internal sealed record DialFrame(
    string Title,
    string Artist,
    string Elapsed,
    string Duration,
    int Progress,
    bool TimelineVisible);

internal static class DialFrameBuilder
{
    private const double RegularCellEm = 0.6;
    private const double SemiboldCellEm = 0.64;

    public const int TitleCells = (int)(TouchZones.TextWidth / (TouchZones.TitleFontSize * SemiboldCellEm));
    public const int ArtistCells = (int)(TouchZones.TextWidth / (TouchZones.ArtistFontSize * RegularCellEm));

    // A scrolling line takes more cells than fit, the layout fades the overflow at the right edge.
    private const double WindowCellEm = 0.45;

    public const int TitleWindowCells = (int)(TouchZones.TextWidth / (TouchZones.TitleFontSize * WindowCellEm));
    public const int ArtistWindowCells = (int)(TouchZones.TextWidth / (TouchZones.ArtistFontSize * WindowCellEm));

    public static DialFrame Build(
        MediaState state,
        MediaTimeline timeline,
        DateTimeOffset now,
        TextDisplayMode mode,
        int marqueeSpeed,
        int marqueeOffset)
    {
        if (!state.IsActive || !state.HasMediaData)
        {
            return new DialFrame("", "", "", "", 0, false);
        }

        var title = Line(NowPlayingText.Title(state, mode), marqueeSpeed, marqueeOffset, TitleCells, TitleWindowCells);
        var artist = Line(NowPlayingText.Artist(state, mode), marqueeSpeed, marqueeOffset, ArtistCells, ArtistWindowCells);

        if (!timeline.HasDuration)
        {
            return new DialFrame(title, artist, "", "", 0, false);
        }

        var position = PlaybackClock.PositionAt(timeline, now);
        var progress = (int)Math.Clamp(Math.Round(position / timeline.Duration * 100), 0, 100);
        return new DialFrame(
            title,
            artist,
            TimeFormat.Format(position),
            TimeFormat.Format(timeline.Duration),
            progress,
            true);
    }

    private static string Line(string text, int marqueeSpeed, int marqueeOffset, int fitCells, int windowCells) =>
        marqueeSpeed > 0 ? TextFit.Window(text, marqueeOffset, fitCells, windowCells) : text;
}
