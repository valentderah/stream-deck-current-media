using System.Text.Json.Serialization;

namespace CurrentMedia.Mac;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AdapterStreamMessage))]
[JsonSerializable(typeof(AdapterPayload))]
internal partial class AdapterJsonContext : JsonSerializerContext;
