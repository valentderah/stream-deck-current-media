using Xunit;

namespace CurrentMedia.Tests;

public class AsyncWaitTests
{
    [Fact]
    public async Task For_CompletedTask_ReturnsValue()
    {
        var result = await AsyncWait.For(Task.FromResult(42), TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(WaitResultKind.Completed, result.Kind);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task For_FinishesBeforeTimeout_ReturnsValue()
    {
        var result = await AsyncWait.For(Delayed(20, 7), TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(WaitResultKind.Completed, result.Kind);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task For_ExceedsTimeout_ReturnsTimedOut()
    {
        var result = await AsyncWait.For(Delayed(500, 1), TimeSpan.FromMilliseconds(20), CancellationToken.None);
        Assert.Equal(WaitResultKind.TimedOut, result.Kind);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task For_PreCanceledToken_ReturnsCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await AsyncWait.For(Delayed(500, 1), TimeSpan.FromSeconds(1), cts.Token);
        Assert.Equal(WaitResultKind.Canceled, result.Kind);
    }

    [Fact]
    public async Task For_CanceledDuringWait_ReturnsCanceled()
    {
        using var cts = new CancellationTokenSource();
        var wait = AsyncWait.For(Delayed(500, 1), TimeSpan.FromSeconds(2), cts.Token);
        cts.CancelAfter(20);
        var result = await wait;
        Assert.Equal(WaitResultKind.Canceled, result.Kind);
    }

    [Fact]
    public async Task For_FaultedTask_Throws()
    {
        var faulted = Task.FromException<int>(new InvalidOperationException("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AsyncWait.For(faulted, TimeSpan.FromSeconds(1), CancellationToken.None));
    }

    [Fact]
    public void Timeouts_MatchSpec()
    {
        Assert.Equal(TimeSpan.FromSeconds(3), MediaClientTimeouts.SessionManagerRequest);
        Assert.Equal(TimeSpan.FromSeconds(1), MediaClientTimeouts.SessionOperation);
    }

    private static async Task<int> Delayed(int delayMs, int value)
    {
        await Task.Delay(delayMs);
        return value;
    }
}
