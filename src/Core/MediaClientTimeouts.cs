namespace CurrentMedia;

internal static class MediaClientTimeouts
{
    public static readonly TimeSpan SessionManagerRequest = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan SessionOperation = TimeSpan.FromSeconds(1);
}
