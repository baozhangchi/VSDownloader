using System.Text.Json.Serialization;

namespace VSDownloader.Models;

[JsonSerializable(typeof(List<BootstrapperInfo>))]
[JsonSerializable(typeof(LanguageInfo))]
[JsonSerializable(typeof(LayoutConfig))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
internal partial class JsonGenerationContext : JsonSerializerContext
{
}