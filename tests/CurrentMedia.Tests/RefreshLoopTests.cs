using Xunit;

namespace CurrentMedia.Tests;

public class RefreshLoopTests
{
    [Fact]
    public async Task Burst_OfRequests_CollapsesIntoSinglePass()
    {
        var passes = 0;
        using var loop = NewLoop(() => Interlocked.Increment(ref passes), quietMs: 60, maxDelayMs: 400, pollMs: 10_000);

        loop.Start();
        for (var i = 0; i < 50; i++)
        {
            loop.Request();
        }

        await Task.Delay(500);
        Assert.InRange(Volatile.Read(ref passes), 1, 2);
    }

    [Fact]
    public async Task ContinuousRequests_StillReachTheWork()
    {
        var passes = 0;
        using var loop = NewLoop(() => Interlocked.Increment(ref passes), quietMs: 40, maxDelayMs: 100, pollMs: 10_000);

        loop.Start();
        using var spam = new CancellationTokenSource();
        var spammer = Task.Run(async () =>
        {
            while (!spam.IsCancellationRequested)
            {
                loop.Request();
                await Task.Delay(5);
            }
        });

        await Task.Delay(500);
        spam.Cancel();
        await spammer;

        Assert.True(Volatile.Read(ref passes) >= 2, $"expected repeated passes, got {passes}");
    }

    [Fact]
    public async Task PollInterval_RefreshesWithoutRequests()
    {
        var passes = 0;
        using var loop = NewLoop(() => Interlocked.Increment(ref passes), quietMs: 10, maxDelayMs: 20, pollMs: 80);

        loop.Start();
        await Task.Delay(400);

        Assert.True(Volatile.Read(ref passes) >= 3, $"expected periodic passes, got {passes}");
    }

    [Fact]
    public async Task Passes_NeverOverlap()
    {
        var active = 0;
        var overlapped = 0;
        using var loop = new RefreshLoop(
            async _ =>
            {
                if (Interlocked.Increment(ref active) > 1)
                {
                    Interlocked.Exchange(ref overlapped, 1);
                }

                await Task.Delay(30);
                Interlocked.Decrement(ref active);
            },
            Options(quietMs: 10, maxDelayMs: 20, pollMs: 20));

        loop.Start();
        for (var i = 0; i < 40; i++)
        {
            loop.Request();
            await Task.Delay(5);
        }

        await Task.Delay(200);
        Assert.Equal(0, Volatile.Read(ref overlapped));
    }

    [Fact]
    public async Task FailingPass_DoesNotStopTheLoop()
    {
        var passes = 0;
        var errors = 0;
        using var loop = new RefreshLoop(
            _ =>
            {
                Interlocked.Increment(ref passes);
                throw new InvalidOperationException("boom");
            },
            Options(quietMs: 10, maxDelayMs: 20, pollMs: 60),
            _ => Interlocked.Increment(ref errors));

        loop.Start();
        await Task.Delay(300);

        Assert.True(Volatile.Read(ref passes) >= 2, $"expected the loop to keep running, got {passes}");
        Assert.True(Volatile.Read(ref errors) >= 2, $"expected failures to be reported, got {errors}");
    }

    [Fact]
    public async Task Dispose_StopsFurtherPasses()
    {
        var passes = 0;
        var loop = NewLoop(() => Interlocked.Increment(ref passes), quietMs: 10, maxDelayMs: 20, pollMs: 40);

        loop.Start();
        await Task.Delay(150);
        loop.Dispose();

        var afterDispose = Volatile.Read(ref passes);
        loop.Request();
        await Task.Delay(150);

        Assert.Equal(afterDispose, Volatile.Read(ref passes));
    }

    [Fact]
    public void Request_BeforeStart_DoesNotThrow()
    {
        using var loop = NewLoop(() => { }, quietMs: 10, maxDelayMs: 20, pollMs: 1_000);
        loop.Request();
    }

    [Fact]
    public void Options_MustBeConsistent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RefreshLoop(
            _ => Task.CompletedTask,
            new RefreshLoopOptions { QuietPeriod = TimeSpan.FromMilliseconds(100), MaxDelay = TimeSpan.FromMilliseconds(50) }));

        Assert.Throws<ArgumentOutOfRangeException>(() => new RefreshLoop(
            _ => Task.CompletedTask,
            new RefreshLoopOptions { PollInterval = TimeSpan.Zero }));
    }

    private static RefreshLoop NewLoop(Action work, int quietMs, int maxDelayMs, int pollMs) =>
        new(
            _ =>
            {
                work();
                return Task.CompletedTask;
            },
            Options(quietMs, maxDelayMs, pollMs));

    private static RefreshLoopOptions Options(int quietMs, int maxDelayMs, int pollMs) => new()
    {
        QuietPeriod = TimeSpan.FromMilliseconds(quietMs),
        MaxDelay = TimeSpan.FromMilliseconds(maxDelayMs),
        PollInterval = TimeSpan.FromMilliseconds(pollMs)
    };
}
