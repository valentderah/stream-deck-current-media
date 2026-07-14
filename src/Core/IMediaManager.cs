namespace CurrentMedia;

public interface IMediaManager : IDisposable
{
    event EventHandler<MediaState>? MediaStateChanged;
    Task InitializeAsync();
    Task RequestUpdateAsync();
    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SeekForwardAsync();
    Task SeekBackwardAsync();
}
