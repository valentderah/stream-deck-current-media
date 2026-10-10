using System.Collections.Concurrent;
using Xunit;

namespace CurrentMedia.Tests;

public class SeekAccumulatorTests
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(150);

    private static SeekAccumulator NewAccumulator(ConcurrentQueue<int> flushed, Action<Exception>? onError = null) =>
        new(seconds =>
        {
            flushed.Enqueue(seconds);
            return Task.CompletedTask;
        }, Quiet, MaxDelay, onError);

    [Fact]
    public async Task Burst_IsFlushedOnceWithTheSum()
    {
        var flushed = new ConcurrentQueue<int>();
        using var accumulator = NewAccumulator(flushed);

        accumulator.Add(5);
        accumulator.Add(5);
        accumulator.Add(5);
        accumulator.Add(-5);
        await Task.Delay(300);

        Assert.Equal(new[] { 10 }, flushed.ToArray());
    }

    [Fact]
    public async Task SeparateBursts_AreFlushedSeparately()
    {
        var flushed = new ConcurrentQueue<int>();
        using var accumulator = NewAccumulator(flushed);

        accumulator.Add(5);
        await Task.Delay(300);
        accumulator.Add(-10);
        await Task.Delay(300);

        Assert.Equal(new[] { 5, -10 }, flushed.ToArray());
    }

    [Fact]
    public async Task ContinuousTicks_AreFlushedBeforeTheyStop()
    {
        var flushed = new ConcurrentQueue<int>();
        using var accumulator = NewAccumulator(flushed);

        for (var i = 0; i < 40; i++)
        {
            accumulator.Add(1);
            await Task.Delay(10);
        }

        Assert.NotEmpty(flushed);
        await Task.Delay(300);
        Assert.Equal(40, flushed.Sum());
    }

    [Fact]
    public async Task ZeroTotal_IsNotFlushed()
    {
        var flushed = new ConcurrentQueue<int>();
        using var accumulator = NewAccumulator(flushed);

        accumulator.Add(5);
        accumulator.Add(5);
        accumulator.Add(5);
        accumulator.Add(-5);
        accumulator.Add(-5);
        accumulator.Add(-5);
        await Task.Delay(300);

        Assert.Empty(flushed);
    }

    [Fact]
    public async Task Dispose_DropsPendingTicks()
    {
        var flushed = new ConcurrentQueue<int>();
        var accumulator = NewAccumulator(flushed);

        accumulator.Add(5);
        accumulator.Dispose();
        accumulator.Add(5);
        await Task.Delay(300);

        Assert.Empty(flushed);
    }

    [Fact]
    public async Task FailingFlush_ReportsTheErrorAndKeepsWorking()
    {
        var errors = new ConcurrentQueue<Exception>();
        var calls = 0;
        using var accumulator = new SeekAccumulator(_ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        }, Quiet, MaxDelay, errors.Enqueue);

        accumulator.Add(5);
        await Task.Delay(300);
        accumulator.Add(5);
        await Task.Delay(300);

        Assert.Single(errors);
        Assert.Equal(2, Volatile.Read(ref calls));
    }
}
