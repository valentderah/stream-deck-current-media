namespace CurrentMedia;

internal readonly struct WaitResult<T>
{
    public WaitResultKind Kind { get; }
    public T? Value { get; }

    private WaitResult(WaitResultKind kind, T? value)
    {
        Kind = kind;
        Value = value;
    }

    public static WaitResult<T> Completed(T value) => new(WaitResultKind.Completed, value);

    public static WaitResult<T> TimedOut() => new(WaitResultKind.TimedOut, default);

    public static WaitResult<T> Canceled() => new(WaitResultKind.Canceled, default);
}
