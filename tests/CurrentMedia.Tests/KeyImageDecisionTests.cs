using CurrentMedia;
using Xunit;

namespace CurrentMedia.Tests;

public class KeyImageDecisionTests
{
    [Theory]
    [InlineData(false, ImagePosition.None, false, KeyImageSource.Transparent)]
    [InlineData(false, ImagePosition.TopLeft, false, KeyImageSource.Transparent)]
    [InlineData(false, ImagePosition.NoImage, false, KeyImageSource.Transparent)]
    [InlineData(false, ImagePosition.None, true, KeyImageSource.IdleBackground)]
    [InlineData(false, ImagePosition.TopLeft, true, KeyImageSource.IdleBackground)]
    [InlineData(false, ImagePosition.NoImage, true, KeyImageSource.IdleBackground)]
    [InlineData(true, ImagePosition.None, true, KeyImageSource.AlbumArt)]
    [InlineData(true, ImagePosition.TopLeft, true, KeyImageSource.AlbumArt)]
    [InlineData(true, ImagePosition.TopRight, false, KeyImageSource.AlbumArt)]
    [InlineData(true, ImagePosition.NoImage, true, KeyImageSource.IdleBackground)]
    [InlineData(true, ImagePosition.NoImage, false, KeyImageSource.Transparent)]
    public void Choose_MatchesSpecTable(
        bool hasMedia,
        ImagePosition position,
        bool hasIdleImage,
        KeyImageSource expected)
    {
        Assert.Equal(expected, KeyImageDecision.Choose(hasMedia, position, hasIdleImage));
    }
}
