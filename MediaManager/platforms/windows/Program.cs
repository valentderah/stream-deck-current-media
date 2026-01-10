using System.Threading.Tasks;

namespace MediaManager.Windows;

class Program
{
    static async Task Main(string[] args)
    {
        await MediaSessionManager.RunAsync();
    }
}
