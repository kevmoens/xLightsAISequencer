using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Refinement;

/// <summary>Mirrors <see cref="ReadOnlyModelIdSetJsonConverter"/>, applied to the nullable <c>IReadOnlySet&lt;Guid&gt;?</c> shape of <see cref="XlightsAI.Studio.Editing.EditScope.EffectIds"/>.</summary>
public sealed class ReadOnlyGuidSetJsonConverter : JsonConverter<IReadOnlySet<Guid>?>
{
    public override IReadOnlySet<Guid>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : JsonSerializer.Deserialize<HashSet<Guid>>(ref reader, options);

    public override void Write(Utf8JsonWriter writer, IReadOnlySet<Guid>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
