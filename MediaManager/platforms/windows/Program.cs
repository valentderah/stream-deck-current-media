using BarRaider.SdTools;

namespace CurrentMedia;

class Program
{
    static void Main(string[] args)
    {
        // Uncomment this line for debugging
        // while (!System.Diagnostics.Debugger.IsAttached) { System.Threading.Thread.Sleep(100); }

        SDWrapper.Run(args);
    }
}
