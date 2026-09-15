namespace CurrentMedia;

internal static class MediaClientTimeouts
{
    public static readonly TimeSpan SessionOperation = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SessionManagerRequest = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan Thumbnail = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan AppIcon = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan ManagerReady = TimeSpan.FromSeconds(4);
}
