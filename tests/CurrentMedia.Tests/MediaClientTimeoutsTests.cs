using Xunit;

namespace CurrentMedia.Tests;

public class MediaClientTimeoutsTests
{
    [Fact]
    public void EverySmtcCallIsBounded()
    {
        Assert.True(MediaClientTimeouts.SessionOperation > TimeSpan.Zero);
        Assert.True(MediaClientTimeouts.SessionManagerRequest > TimeSpan.Zero);
        Assert.True(MediaClientTimeouts.Thumbnail > TimeSpan.Zero);
        Assert.True(MediaClientTimeouts.AppIcon > TimeSpan.Zero);
        Assert.True(MediaClientTimeouts.ManagerReady > TimeSpan.Zero);
    }

    [Fact]
    public void ManagerReady_CoversAnAcquisitionAttempt()
    {
        Assert.True(MediaClientTimeouts.ManagerReady >= MediaClientTimeouts.SessionManagerRequest);
    }

    [Fact]
    public void Timeouts_StayWithinOneRefreshBudget()
    {
        var options = new RefreshLoopOptions();
        var slowest = new[]
        {
            MediaClientTimeouts.SessionOperation,
            MediaClientTimeouts.Thumbnail,
            MediaClientTimeouts.AppIcon
        }.Max();

        Assert.True(slowest < options.PollInterval);
    }

    [Fact]
    public void ElectronFriendlyBudgets()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), MediaClientTimeouts.SessionOperation);
        Assert.Equal(TimeSpan.FromSeconds(8), new RefreshLoopOptions().PollInterval);
    }
}
