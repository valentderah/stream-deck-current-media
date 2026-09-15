using BarRaider.SdTools;

namespace CurrentMedia;

class Program
{
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => MediaManagerProvider.Shutdown();
        try
        {
            SDWrapper.Run(args);
        }
        finally
        {
            MediaManagerProvider.Shutdown();
        }
    }
}
