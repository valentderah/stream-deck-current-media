using Xunit;

namespace CurrentMedia.Tests;

public class MediaCommandTests
{
    [Theory]
    [InlineData(ActionType.Toggle, "toggle")]
    [InlineData(ActionType.Next, "next")]
    [InlineData(ActionType.Previous, "previous")]
    [InlineData(ActionType.Forward, "seekBy:7")]
    [InlineData(ActionType.Backward, "seekBy:-7")]
    public async Task RunAsync_CallsTheMatchingManagerMethod(ActionType action, string expected)
    {
        var manager = new FakeMediaManager();

        await MediaCommand.RunAsync(manager, action, 7);

        Assert.Equal(new[] { expected }, manager.Calls);
    }

    [Fact]
    public async Task RunAsync_None_CallsNothing()
    {
        var manager = new FakeMediaManager();

        await MediaCommand.RunAsync(manager, ActionType.None, 7);

        Assert.Empty(manager.Calls);
    }

    [Fact]
    public void RunAsync_UnknownAction_Throws()
    {
        var manager = new FakeMediaManager();

        Assert.Throws<InvalidOperationException>(() => { _ = MediaCommand.RunAsync(manager, (ActionType)99, 7); });
    }
}
