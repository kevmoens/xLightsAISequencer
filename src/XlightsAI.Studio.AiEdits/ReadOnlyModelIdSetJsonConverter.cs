using System.Text.Json;
using System.Text.Json.Serialization;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// Mirrors <see cref="XlightsAI.Studio.Display.ReadOnlyStringSetJsonConverter"/>'s own convention,
/// applied to <see cref="EditScope.Models"/> (a <c>ModelId</c> set rather than a string set) so a
/// <see cref="SelectionContext"/> -- the first type in this project to ever persist an
/// <c>EditScope</c> -- can round-trip through <see cref="System.Text.Json"/> without widening
/// <c>EditScope.Models</c>'s own already-accepted <c>IReadOnlySet&lt;ModelId&gt;</c> type.
/// </summary>
public sealed class ReadOnlyModelIdSetJsonConverter : JsonConverter<IReadOnlySet<ModelId>>
{
    public override IReadOnlySet<ModelId> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<HashSet<ModelId>>(ref reader, options) ?? [];

    public override void Write(Utf8JsonWriter writer, IReadOnlySet<ModelId> value, JsonSerializerOptions options)
    {
        // Writes each element individually (type ModelId, not IReadOnlySet<ModelId>) rather than
        // delegating the whole set back to JsonSerializer.Serialize(writer, value, options) -- this
        // converter is registered in Options.Converters (JsonSelectionContextStore), so re-serializing
        // the interface-typed value against the same options would re-resolve this very converter and
        // recurse infinitely (confirmed by a real stack-overflow test run before this fix).
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
