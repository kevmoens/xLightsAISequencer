using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// The metadata schema's <c>default</c>/<c>defaultX</c>/<c>defaultY</c> fields are untyped ("type depends
/// on the 'type' field" per <c>_schema.json</c>) — a string, a number, or a bool depending on the
/// property. This converter boxes whatever JSON literal is present into the matching CLR primitive so
/// callers can format it back out without re-parsing a <see cref="JsonElement"/>.
/// </summary>
internal sealed class JsonElementBoxingConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
            _ => throw new JsonException($"Unsupported default-value token type '{reader.TokenType}'."),
        };

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), options);
}
