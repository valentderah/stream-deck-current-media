using Newtonsoft.Json;

namespace CurrentMedia.Actions;

internal sealed class SeekActionSettings
{
    public static SeekActionSettings CreateDefault() => new();

    [JsonProperty(PropertyName = "seconds")]
    public int Seconds { get; set; } = SeekSeconds.Default;
}
