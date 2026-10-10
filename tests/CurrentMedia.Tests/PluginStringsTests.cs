using Newtonsoft.Json.Linq;
using Xunit;

namespace CurrentMedia.Tests;

public class PluginStringsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "current-media-tests-" + Guid.NewGuid().ToString("N"));

    public PluginStringsTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "win"));
        File.WriteAllText(Path.Combine(_root, "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "en.json"), """{ "Localization": { "PlayPause": "Play/Pause", "None": "None" } }""");
        File.WriteAllText(Path.Combine(_root, "ru.json"), """{ "Localization": { "PlayPause": "Пауза" } }""");
        File.WriteAllText(Path.Combine(_root, "de.json"), "not json");
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Get_UsesTheRequestedLanguage()
    {
        Assert.Equal("Пауза", PluginStrings.Load(_root, "ru").Get("PlayPause"));
    }

    [Fact]
    public void Get_FallsBackToEnglishForAMissingKey()
    {
        Assert.Equal("None", PluginStrings.Load(_root, "ru").Get("None"));
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("de")]
    [InlineData(null)]
    [InlineData("")]
    public void Get_MissingOrBrokenCatalog_FallsBackToEnglish(string? language)
    {
        Assert.Equal("Play/Pause", PluginStrings.Load(_root, language).Get("PlayPause"));
    }

    [Fact]
    public void Get_UnknownKey_ReturnsTheKey()
    {
        Assert.Equal("Nope", PluginStrings.Load(_root, "en").Get("Nope"));
    }

    [Fact]
    public void Load_WithoutAPluginRoot_ReturnsKeys()
    {
        Assert.Equal("PlayPause", PluginStrings.Load(null, "en").Get("PlayPause"));
    }

    [Fact]
    public void FindPluginRoot_WalksUpToTheManifest()
    {
        Assert.Equal(_root, PluginStrings.FindPluginRoot(Path.Combine(_root, "win")));
        Assert.Null(PluginStrings.FindPluginRoot(Path.GetPathRoot(_root)!));
    }

    [Fact]
    public void EveryCatalog_HasEveryEnglishKey()
    {
        var english = Keys(Path.Combine(RepoPaths.Assets, "en.json"));
        foreach (var file in Directory.GetFiles(RepoPaths.Assets, "*.json"))
        {
            if (Path.GetFileName(file) is "manifest.json" or "en.json")
            {
                continue;
            }

            var missing = english.Except(Keys(file)).ToArray();
            Assert.True(missing.Length == 0, $"{Path.GetFileName(file)} is missing: {string.Join(", ", missing)}");
        }
    }

    [Theory]
    [InlineData("DialRotate")]
    [InlineData("DialPressRotate")]
    [InlineData("DialPress")]
    [InlineData("TouchTap")]
    [InlineData("TouchLongTap")]
    [InlineData("SeekInTrack")]
    [InlineData("SwitchTrack")]
    [InlineData("SeekStep")]
    [InlineData("TapSeek")]
    [InlineData("SectionKey")]
    [InlineData("SectionDial")]
    [InlineData("SectionTouchStrip")]
    [InlineData("SectionDisplay")]
    public void EnglishCatalog_HasTheDialKeys(string key)
    {
        Assert.Contains(key, Keys(Path.Combine(RepoPaths.Assets, "en.json")));
    }

    private static HashSet<string> Keys(string path) =>
        ((JObject)JObject.Parse(File.ReadAllText(path))["Localization"]!)
        .Properties()
        .Select(property => property.Name)
        .ToHashSet();
}
