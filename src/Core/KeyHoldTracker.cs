namespace CurrentMedia;

internal sealed class KeyHoldTracker
{
    private readonly object _lock = new();
    private int _press;
    private bool _down;
    private bool _held;

    public int Down()
    {
        lock (_lock)
        {
            _down = true;
            _held = false;
            return ++_press;
        }
    }

    // The press id keeps a late timer of an earlier press from firing on the current one.
    public bool Hold(int press)
    {
        lock (_lock)
        {
            if (!_down || _held || press != _press)
            {
                return false;
            }

            _held = true;
            return true;
        }
    }

    public bool Up()
    {
        lock (_lock)
        {
            var fire = _down && !_held;
            _down = false;
            _held = false;
            return fire;
        }
    }
}
