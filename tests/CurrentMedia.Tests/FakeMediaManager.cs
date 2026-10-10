using System.Globalization;

namespace CurrentMedia.Tests;

internal sealed class FakeMediaManager : IMediaManager
{
    public List<string> Calls { get; } = new();

    public event EventHandler<MediaState>? MediaStateChanged;

    public event EventHandler<MediaTimeline>? TimelineChanged;

    public Task InitializeAsync() => Record("init");

    public Task RequestUpdateAsync() => Record("update");

    public Task PlayPauseAsync() => Record("toggle");

    public Task NextAsync() => Record("next");

    public Task PreviousAsync() => Record("previous");

    public Task SeekByAsync(int offsetSeconds) => Record($"seekBy:{offsetSeconds}");

    public Task SeekToAsync(double seconds) =>
        Record(string.Create(CultureInfo.InvariantCulture, $"seekTo:{seconds}"));

    public void RaiseState(MediaState state) => MediaStateChanged?.Invoke(this, state);

    public void RaiseTimeline(MediaTimeline timeline) => TimelineChanged?.Invoke(this, timeline);

    public void Dispose()
    {
    }

    private Task Record(string call)
    {
        Calls.Add(call);
        return Task.CompletedTask;
    }
}
