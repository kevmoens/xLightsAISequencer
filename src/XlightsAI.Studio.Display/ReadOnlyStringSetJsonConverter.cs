using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Display;

/// <summary>
/// <see cref="System.Text.Json"/>'s default reflection-based serializer cannot deserialize into
/// <see cref="IReadOnlySet{T}"/> (no settable concrete collection to populate) -- required because
/// <see cref="DisplayNode.Tags"/> keeps Section 3's own proposed <c>IReadOnlySet&lt;string&gt;</c> type
/// rather than widening it to a list just to satisfy persistence. Reads/writes as a plain JSON string
/// array, backed by a <see cref="HashSet{T}"/>.
/// </summary>
public sealed class ReadOnlyStringSetJsonConverter : JsonConverter<IReadOnlySet<string>>
{
    public override IReadOnlySet<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<HashSet<string>>(ref reader, options) ?? [];

    public override void Write(Utf8JsonWriter writer, IReadOnlySet<string> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
