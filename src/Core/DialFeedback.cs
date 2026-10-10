using Newtonsoft.Json.Linq;

namespace CurrentMedia;

internal sealed class DialFeedback
{
    private DialFrame? _sent;
    private string? _coverKey;
    private string? _backgroundKey;

    public JObject? Next(
        DialFrame frame,
        string coverKey,
        Func<string> renderCover,
        string backgroundKey,
        Func<string> renderBackground)
    {
        var payload = new JObject();
        var previous = _sent;

        if (previous == null || previous.Title != frame.Title)
        {
            payload["title"] = frame.Title;
        }

        if (previous == null || previous.Artist != frame.Artist)
        {
            payload["artist"] = frame.Artist;
        }

        var visibilityChanged = previous == null || previous.TimelineVisible != frame.TimelineVisible;

        if (visibilityChanged || previous!.Elapsed != frame.Elapsed)
        {
            payload["elapsed"] = Item(frame.Elapsed, frame.TimelineVisible);
        }

        if (visibilityChanged || previous!.Duration != frame.Duration)
        {
            payload["duration"] = Item(frame.Duration, frame.TimelineVisible);
        }

        if (visibilityChanged || previous!.Progress != frame.Progress)
        {
            payload["progress"] = Item(frame.Progress, frame.TimelineVisible);
        }

        if (_coverKey != coverKey)
        {
            payload["cover"] = renderCover();
            _coverKey = coverKey;
        }

        if (_backgroundKey != backgroundKey)
        {
            payload["background"] = renderBackground();
            _backgroundKey = backgroundKey;
        }

        _sent = frame;
        return payload.Count == 0 ? null : payload;
    }

    public void Reset()
    {
        _sent = null;
        _coverKey = null;
        _backgroundKey = null;
    }

    private static JObject Item(JToken value, bool enabled) => new()
    {
        ["value"] = value,
        ["enabled"] = enabled
    };
}
