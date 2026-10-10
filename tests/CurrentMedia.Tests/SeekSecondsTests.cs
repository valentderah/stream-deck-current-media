using Xunit;

namespace CurrentMedia.Tests;

public class SeekSecondsTests
{
    [Fact]
    public void Default_IsTen()
    {
        Assert.Equal(10, SeekSeconds.Default);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    [InlineData(120, 120)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void Normalize_Positive_ReturnsSameValue(int input, int expected)
    {
        Assert.Equal(expected, SeekSeconds.Normalize(input));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(int.MinValue)]
    public void Normalize_NonPositive_ReturnsDefault(int input)
    {
        Assert.Equal(SeekSeconds.Default, SeekSeconds.Normalize(input));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(15, 15)]
    [InlineData(0, 5)]
    [InlineData(-3, 5)]
    public void Normalize_WithFallback_UsesItForNonPositiveValues(int input, int expected)
    {
        Assert.Equal(expected, SeekSeconds.Normalize(input, SeekSeconds.DialDefault));
    }
}
