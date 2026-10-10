using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CurrentMedia;

internal sealed class PluginStrings
{
    private const string FallbackLanguage = "en";
    private const int MaxParentLevels = 5;

    private readonly Dictionary<string, string> _primary;
    private readonly Dictionary<string, string> _fallback;

    private PluginStrings(Dictionary<string, string> primary, Dictionary<string, string> fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public static PluginStrings Load(string? pluginRoot, string? language)
    {
        if (string.IsNullOrEmpty(pluginRoot))
        {
            return new PluginStrings(new Dictionary<string, string>(), new Dictionary<string, string>());
        }

        var fallback = ReadCatalog(Path.Combine(pluginRoot, FallbackLanguage + ".json"));
        var primary = string.IsNullOrEmpty(language) || language == FallbackLanguage
            ? fallback
            : ReadCatalog(Path.Combine(pluginRoot, language + ".json"));
        return new PluginStrings(primary, fallback);
    }

    public static string? FindPluginRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        for (var level = 0; directory != null && level <= MaxParentLevels; level++)
        {
            if (File.Exists(Path.Combine(directory.FullName, "manifest.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    public string Get(string key)
    {
        if (_primary.TryGetValue(key, out var value) && value.Length > 0)
        {
            return value;
        }

        return _fallback.TryGetValue(key, out var fallback) && fallback.Length > 0 ? fallback : key;
    }

    private static Dictionary<string, string> ReadCatalog(string path)
    {
        var result = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(path))
            {
                return result;
            }

            if (JObject.Parse(File.ReadAllText(path))["Localization"] is not JObject localization)
            {
                return result;
            }

            foreach (var property in localization.Properties())
            {
                if (property.Value.Type == JTokenType.String)
                {
                    result[property.Name] = (string)property.Value!;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            result.Clear();
        }

        return result;
    }
}
