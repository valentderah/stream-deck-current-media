namespace CurrentMedia;

internal static class SeekSeconds
{
    public const int Default = 10;

    public static int Normalize(int seconds) => seconds >= 1 ? seconds : Default;
}
