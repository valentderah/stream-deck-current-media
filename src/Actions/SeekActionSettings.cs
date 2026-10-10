using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CurrentMedia.Actions;

internal sealed class SeekActionSettings
{
    public static SeekActionSettings CreateDefault() => new();

    public static SeekActionSettings FromPayload(JObject? settings)
    {
        if (settings == null || settings.Count == 0)
        {
            return CreateDefault();
        }

        try
        {
            return settings.ToObject<SeekActionSettings>() ?? CreateDefault();
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
    }

    [JsonProperty(PropertyName = "seconds")]
    public int Seconds { get; set; } = SeekSeconds.Default;
}
