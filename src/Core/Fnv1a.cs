using System.Globalization;

namespace CurrentMedia;

internal static class Fnv1a
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    public static uint Hash32(string? text) => Hash32(text.AsSpan());

    public static uint Hash32(ReadOnlySpan<char> text)
    {
        var hash = OffsetBasis;
        for (var i = 0; i < text.Length; i++)
        {
            hash ^= (byte)text[i];
            hash *= Prime;
        }

        return hash;
    }

    public static string Hash32Hex(string? text) =>
        Hash32(text).ToString("x8", CultureInfo.InvariantCulture);
}
