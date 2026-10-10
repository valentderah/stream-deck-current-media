namespace CurrentMedia.Tests;

internal static class RepoPaths
{
    public static string Assets { get; } = FindAssets();

    private static string FindAssets()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "assets");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("assets/manifest.json not found above " + AppContext.BaseDirectory);
    }
}
