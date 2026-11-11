using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Loads the shippable <see cref="EffectVisualCharacterCatalog"/> (build-plan Phase 18 §6, Phase E) from
/// the <c>EmbeddedResource</c> baked into this assembly at
/// <c>src/XlightsAI.Studio.Knowledge/SeedData/effect-visual-characters.json</c> -- same convention, and same
/// rationale, as <see cref="MultiLayerTemplateCatalogLoader"/>: embedded so it ships inside the DLL with no
/// dependency on a loose sibling file surviving <c>dotnet publish</c>, and because this is core, rarely-
/// changing, committed knowledge (regenerated only by <c>tools/EffectCatalogBuilder</c> when the effect
/// vocabulary changes, then committed like any other hand-curated knowledge artifact) -- never a per-machine
/// <c>%AppData%</c> runtime artifact the way <c>CraftRules</c>/<c>ExemplarIndex</c> are.
/// </summary>
public static class EffectVisualCharacterCatalogLoader
{
    private const string ResourceName = "XlightsAI.Studio.Knowledge.SeedData.effect-visual-characters.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<EffectVisualCharacterCatalog> Cached = new(LoadFromEmbeddedResource);

    /// <summary>The shippable catalog, parsed once and cached for the process lifetime -- it is immutable seed data, never regenerated per call.</summary>
    public static EffectVisualCharacterCatalog Load() => Cached.Value;

    private static EffectVisualCharacterCatalog LoadFromEmbeddedResource()
    {
        var assembly = typeof(EffectVisualCharacterCatalogLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found in assembly '{assembly.FullName}'. " +
                "Confirm it is still declared as an <EmbeddedResource> in XlightsAI.Studio.Knowledge.csproj.");

        var catalog = JsonSerializer.Deserialize<EffectVisualCharacterCatalog>(stream, JsonOptions);
        return catalog ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' deserialized to null.");
    }
}
