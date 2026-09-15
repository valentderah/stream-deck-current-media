namespace CurrentMedia;

internal static class AsyncWait
{
    public static async Task<WaitResult<T>> For<T>(
        Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (cancellationToken.IsCancellationRequested)
        {
            return WaitResult<T>.Canceled();
        }

        if (task.IsCompleted)
        {
            return WaitResult<T>.Completed(await task.ConfigureAwait(false));
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var delayTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);

        Task winner;
        try
        {
            winner = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return WaitResult<T>.Canceled();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return WaitResult<T>.Canceled();
        }

        if (winner != task)
        {
            return WaitResult<T>.TimedOut();
        }

        return WaitResult<T>.Completed(await task.ConfigureAwait(false));
    }
}
