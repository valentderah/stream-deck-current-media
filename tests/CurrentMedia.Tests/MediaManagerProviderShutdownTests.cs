using Xunit;

namespace CurrentMedia.Tests;

public class MediaManagerProviderShutdownTests
{
    [Fact]
    public void Shutdown_BeforeInstance_DoesNotThrow()
    {
        MediaManagerProvider.Shutdown();
    }

    [Fact]
    public void Shutdown_TwiceBeforeInstance_DoesNotThrow()
    {
        MediaManagerProvider.Shutdown();
        MediaManagerProvider.Shutdown();
    }
}
