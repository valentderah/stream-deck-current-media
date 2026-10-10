namespace CurrentMedia;

public interface IMediaManager : IDisposable
{
    event EventHandler<MediaState>? MediaStateChanged;
    event EventHandler<MediaTimeline>? TimelineChanged;
    Task InitializeAsync();
    Task RequestUpdateAsync();
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SeekByAsync(int offsetSeconds);
    Task SeekToAsync(double seconds);
}
