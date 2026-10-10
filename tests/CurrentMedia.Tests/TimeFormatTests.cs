using Xunit;

namespace CurrentMedia.Tests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(59.9, "0:59")]
    [InlineData(60, "1:00")]
    [InlineData(83, "1:23")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(5025, "1:23:45")]
    [InlineData(-4, "0:00")]
    [InlineData(double.NaN, "0:00")]
    [InlineData(double.PositiveInfinity, "0:00")]
    public void Format_ProducesClockText(double seconds, string expected)
    {
        Assert.Equal(expected, TimeFormat.Format(seconds));
    }
}
