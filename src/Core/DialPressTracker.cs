namespace CurrentMedia;

internal sealed class DialPressTracker
{
    private bool _down;
    private bool _rotated;

    public void Down()
    {
        _down = true;
        _rotated = false;
    }

    public void Rotated()
    {
        if (_down)
        {
            _rotated = true;
        }
    }

    public bool Up()
    {
        var fire = _down && !_rotated;
        _down = false;
        _rotated = false;
        return fire;
    }
}
