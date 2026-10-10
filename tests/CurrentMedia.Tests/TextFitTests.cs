using Xunit;

namespace CurrentMedia.Tests;

public class TextFitTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("Song", 4)]
    [InlineData("Песня", 5)]
    [InlineData("เพลง", 4)]
    [InlineData("日本語", 6)]
    [InlineData("노래", 4)]
    [InlineData("ＡＢ", 4)]
    [InlineData("🎵🎵", 4)]
    [InlineData("é", 1)]
    [InlineData("a​b", 2)]
    public void Cells_CountsWideCharactersTwiceAndMarksNotAtAll(string text, int expected)
    {
        Assert.Equal(expected, TextFit.Cells(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Song")]
    [InlineData("日本語")]
    public void Window_TextThatFits_IsUnchanged(string text)
    {
        Assert.Equal(text, TextFit.Window(text, 5, 6));
    }

    [Theory]
    [InlineData("A very long song title", 0, "A very lon")]
    [InlineData("A very long song title", 2, "very long ⁠")]
    [InlineData("Очень длинное название", 6, "длинное на")]
    [InlineData("とても長い曲のタイトル", 1, "ても長い曲")]
    [InlineData("a日本語の長いタイトル", 0, "a日本語の")]
    public void Window_LongText_TakesAsManyCellsAsFit(string text, int offset, string expected)
    {
        Assert.Equal(expected, TextFit.Window(text, offset, 10));
    }

    [Fact]
    public void Window_PastTheEnd_ShowsTheGapAndWrapsAround()
    {
        const string text = "A very long song title";

        Assert.Equal("le   A ver", TextFit.Window(text, 20, 10));
        Assert.Equal(TextFit.Window(text, 2, 10), TextFit.Window(text, 2 + text.Length + 3, 10));
    }

    [Theory]
    [InlineData(4, "⁠ Up Call   ⁠")]
    [InlineData(12, "⁠   Wake Up ⁠")]
    [InlineData(14, "⁠ Wake Up Ca")]
    public void Window_EdgeWhitespace_IsGuardedAgainstTrimming(int offset, string expected)
    {
        Assert.Equal(expected, TextFit.Window("Wake Up Call", offset, 11));
    }

    [Fact]
    public void Window_WiderThanTheFitLimit_OverfillsAndKeepsItsWidth()
    {
        const string text = "Wake Up Call";

        Assert.Equal("Wake Up Cal", TextFit.Window("Wake Up Cal", 3, 11, 16));
        Assert.Equal("⁠ Wake Up Call   ⁠", TextFit.Window(text, 14, 11, 16));
        for (var offset = 0; offset < 30; offset++)
        {
            Assert.Equal(16, TextFit.Cells(TextFit.Window(text, offset, 11, 16)));
        }
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("Song", "Song")]
    [InlineData(" Song", "⁠ Song")]
    [InlineData("Song ", "Song ⁠")]
    [InlineData("　曲", "⁠　曲")]
    public void GuardEdges_WrapsOnlyWhitespaceEdges(string text, string expected)
    {
        Assert.Equal(expected, TextFit.GuardEdges(text));
    }

    [Fact]
    public void Window_NeverSplitsAGrapheme()
    {
        var text = string.Concat(Enumerable.Repeat("🎵é", 8));

        for (var offset = 0; offset < 30; offset++)
        {
            var window = TextFit.Window(text, offset, 7);

            Assert.False(char.IsLowSurrogate(window[0]));
            Assert.False(char.IsHighSurrogate(window[^1]));
            Assert.NotEqual('́', window[0]);
            Assert.True(TextFit.Cells(window) <= 7);
        }
    }
}
