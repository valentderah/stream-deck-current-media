namespace CurrentMedia;

public enum KeyImageSource
{
    Transparent,
    IdleBackground,
    AlbumArt
}

public static class KeyImageDecision
{
    public static KeyImageSource Choose(bool hasMedia, ImagePosition position, bool hasIdleImage)
    {
        if (!hasMedia)
        {
            return hasIdleImage ? KeyImageSource.IdleBackground : KeyImageSource.Transparent;
        }

        if (position == ImagePosition.NoImage)
        {
            return hasIdleImage ? KeyImageSource.IdleBackground : KeyImageSource.Transparent;
        }

        return KeyImageSource.AlbumArt;
    }
}
