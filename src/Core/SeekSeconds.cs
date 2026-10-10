namespace CurrentMedia;

internal static class SeekSeconds
{
    public const int Default = 10;

    public static int Normalize(int seconds) => seconds >= 1 ? seconds : Default;

    public const int DialDefault = 5;

    public static int Normalize(int seconds, int fallback) => seconds >= 1 ? seconds : fallback;
}
