using System.Globalization;
using Xunit;

namespace CurrentMedia.Tests;

public class Fnv1aTests
{
    [Fact]
    public void Empty_IsOffsetBasis()
    {
        Assert.Equal(0x811c9dc5u, Fnv1a.Hash32(ReadOnlySpan<char>.Empty));
        Assert.Equal(0x811c9dc5u, Fnv1a.Hash32(""));
        Assert.Equal(0x811c9dc5u, Fnv1a.Hash32((string?)null));
        Assert.Equal("811c9dc5", Fnv1a.Hash32Hex(""));
    }

    [Fact]
    public void EqualLength_DifferentBytes_Differ()
    {
        Assert.NotEqual(Fnv1a.Hash32("abcd"), Fnv1a.Hash32("abce"));
        Assert.NotEqual(Fnv1a.Hash32Hex("abcd"), Fnv1a.Hash32Hex("abce"));
    }

    [Fact]
    public void Hash32Hex_IsLowercaseX8()
    {
        var hex = Fnv1a.Hash32Hex("x");
        Assert.Equal(8, hex.Length);
        Assert.Equal(hex, hex.ToLowerInvariant());
        Assert.Equal(hex, Fnv1a.Hash32("x").ToString("x8", CultureInfo.InvariantCulture));
    }
}
