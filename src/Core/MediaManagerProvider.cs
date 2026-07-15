namespace CurrentMedia;

public static class MediaManagerProvider
{
    private static readonly Lazy<IMediaManager> _instance = new(CreateInstance);
    public static IMediaManager Instance => _instance.Value;

    private static IMediaManager CreateInstance()
    {
#if WINDOWS
        return new Windows.WindowsMediaManager();
#else
        return new Mac.MacMediaManager();
#endif
    }
}
