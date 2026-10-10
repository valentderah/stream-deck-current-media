using CurrentMedia.Imaging;

namespace CurrentMedia;

internal static class NowPlayingText
{
    public static string Title(MediaState state, TextDisplayMode mode) =>
        mode is TextDisplayMode.Both or TextDisplayMode.Title ? state.Title : "";

    public static string Artist(MediaState state, TextDisplayMode mode)
    {
        if (mode is not (TextDisplayMode.Both or TextDisplayMode.Artists))
        {
            return "";
        }

        return state.Artists.Count > 0 ? string.Join(", ", state.Artists) : state.Artist;
    }

    public static string KeyTitle(MediaState state, TextDisplayMode mode, int marqueeSpeed, int marqueeOffset)
    {
        if (!state.IsActive || !state.HasMediaData)
        {
            return "";
        }

        var lines = new List<string>(2);
        foreach (var line in new[] { Title(state, mode), Artist(state, mode) })
        {
            if (!string.IsNullOrEmpty(line))
            {
                lines.Add(marqueeSpeed > 0 ? MarqueeHelper.GetMarqueeText(line, marqueeOffset) : line);
            }
        }

        return string.Join("\n", lines);
    }
}
