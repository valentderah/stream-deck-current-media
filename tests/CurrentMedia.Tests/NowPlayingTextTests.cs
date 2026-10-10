using Xunit;

namespace CurrentMedia.Tests;

public class NowPlayingTextTests
{
    private static MediaState Song() => new()
    {
        IsActive = true,
        Title = "Song",
        Artist = "Solo",
        Artists = new List<string> { "A", "B" },
        Status = "Playing"
    };

    [Theory]
    [InlineData(TextDisplayMode.Both, "Song", "A, B")]
    [InlineData(TextDisplayMode.Title, "Song", "")]
    [InlineData(TextDisplayMode.Artists, "", "A, B")]
    [InlineData(TextDisplayMode.None, "", "")]
    public void Lines_FollowTheDisplayMode(TextDisplayMode mode, string title, string artist)
    {
        Assert.Equal(title, NowPlayingText.Title(Song(), mode));
        Assert.Equal(artist, NowPlayingText.Artist(Song(), mode));
    }

    [Fact]
    public void Artist_FallsBackToTheSingleArtistField()
    {
        var state = Song();
        state.Artists = new List<string>();

        Assert.Equal("Solo", NowPlayingText.Artist(state, TextDisplayMode.Both));
    }

    [Fact]
    public void KeyTitle_JoinsNonEmptyLines()
    {
        Assert.Equal("Song\nA, B", NowPlayingText.KeyTitle(Song(), TextDisplayMode.Both, 0, 0));
        Assert.Equal("Song", NowPlayingText.KeyTitle(Song(), TextDisplayMode.Title, 0, 0));
    }

    [Fact]
    public void KeyTitle_InactiveState_IsEmpty()
    {
        Assert.Equal("", NowPlayingText.KeyTitle(new MediaState(), TextDisplayMode.Both, 0, 0));
    }

    [Fact]
    public void KeyTitle_WithMarquee_WindowsLongLines()
    {
        var state = Song();
        state.Title = "A very long song title";

        var text = NowPlayingText.KeyTitle(state, TextDisplayMode.Title, 40, 2);

        Assert.Equal("very long ⁠", text);
    }
}
