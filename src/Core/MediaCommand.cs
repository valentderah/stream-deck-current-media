namespace CurrentMedia;

internal static class MediaCommand
{
    public static Task RunAsync(IMediaManager manager, ActionType action, int seekSeconds) => action switch
    {
        ActionType.Toggle => manager.PlayPauseAsync(),
        ActionType.Next => manager.NextAsync(),
        ActionType.Previous => manager.PreviousAsync(),
        ActionType.Forward => manager.SeekByAsync(seekSeconds),
        ActionType.Backward => manager.SeekByAsync(-seekSeconds),
        ActionType.None => Task.CompletedTask,
        _ => throw new InvalidOperationException($"Unhandled action: {action}")
    };
}
