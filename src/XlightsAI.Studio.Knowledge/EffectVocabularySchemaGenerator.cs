using System.Text.Json.Nodes;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2a: generates a JSON Schema per effect an LLM's structured-output call can be constrained
/// against, expressing valid property names, semantic type, enum of valid choices, and numeric
/// min/max — architecture §8a's "generated, machine-checkable vocabulary" turned into something a
/// provider's native structured-output mechanism can enforce directly.
/// <para>
/// One schema per effect, rather than one schema for the whole vocabulary: each schema merges that
/// effect's own properties with the three shared panels (Buffer/Blending/Color, including the legacy
/// LayerMethod/LayerMorph/EffectLayerMix exceptions and the 16 palette slots) every effect can also
/// carry, because a single Choreographer call about one effect instance needs all of those in one flat
/// object (architecture §2: "keep schemas flat, wide, shallow and short per call").
/// </para>
/// </summary>
public sealed class EffectVocabularySchemaGenerator
{
    private readonly EffectVocabularyCatalog _catalog;

    public EffectVocabularySchemaGenerator(EffectVocabularyCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Generates one JSON Schema document per known effect, keyed by effect name.</summary>
    public IReadOnlyDictionary<string, JsonObject> GenerateAllSchemas() =>
        _catalog.Effects.Keys.ToDictionary(name => name, GenerateSchema, StringComparer.Ordinal);

    public JsonObject GenerateSchema(string effectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectName);
        if (!_catalog.Effects.TryGetValue(effectName, out var effect))
        {
            throw new ArgumentException($"'{effectName}' is not a known effect.", nameof(effectName));
        }

        var properties = new JsonObject();
        foreach (var property in effect.Properties
            .Concat(_catalog.Buffer.Properties)
            .Concat(_catalog.Blending.Properties)
            .Concat(_catalog.Color.Properties))
        {
            properties[property.SettingKey] = BuildPropertySchema(property);
        }

        return new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["title"] = $"xLights '{effectName}' effect settings",
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject BuildPropertySchema(EffectPropertyVocabulary property)
    {
        var schema = new JsonObject
        {
            ["type"] = MapJsonSchemaType(property.SemanticType),
        };

        if (property.Options is { Count: > 0 })
        {
            schema["enum"] = new JsonArray(property.Options.Select(o => (JsonNode)o).ToArray());
        }

        if (property.Min is { } min)
        {
            schema["minimum"] = min;
        }

        if (property.Max is { } max)
        {
            schema["maximum"] = max;
        }

        return schema;
    }

    private static string MapJsonSchemaType(string semanticType) => semanticType switch
    {
        "int" or "float" => "number",
        "bool" => "boolean",
        "enum" or "string" or "color" or "file" or "font" => "string",
        // Judgment call: any future/unrecognized semantic type degrades to "string" rather than
        // throwing, since a schema that is merely permissive is safer than one that fails to generate.
        _ => "string",
    };
}
