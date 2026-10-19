using System.Text.Json;
using System.Text.Json.Serialization;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// Mirrors <see cref="XlightsAI.Studio.AiEdits.ReadOnlyModelIdSetJsonConverter"/>'s own convention,
/// applied here to <see cref="XlightsAI.Studio.Editing.EditScope.Models"/> so <see cref="LoopRun"/> --
/// the first type in this project to ever persist an <c>EditScope</c> -- can round-trip through
/// <see cref="System.Text.Json"/> without widening <c>EditScope.Models</c>'s own already-accepted
/// <c>IReadOnlySet&lt;ModelId&gt;</c> type. Each feature project (<c>AiEdits</c>, here) owns its own copy
/// of this converter rather than sharing one across a project reference, the same already-established
/// convention <c>XlightsAI.Studio.Display</c>'s own <c>ReadOnlyStringSetJsonConverter</c> set.
/// </summary>
public sealed class ReadOnlyModelIdSetJsonConverter : JsonConverter<IReadOnlySet<ModelId>>
{
    public override IReadOnlySet<ModelId> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<HashSet<ModelId>>(ref reader, options) ?? [];

    public override void Write(Utf8JsonWriter writer, IReadOnlySet<ModelId> value, JsonSerializerOptions options)
    {
        // Writes each element individually rather than delegating the whole set back to
        // JsonSerializer.Serialize(writer, value, options) -- this converter is registered in
        // Options.Converters, so re-serializing the interface-typed value against the same options
        // would re-resolve this very converter and recurse infinitely.
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
