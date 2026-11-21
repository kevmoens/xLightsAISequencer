using System.Reflection;
using System.Text.Json;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Loads the shippable <see cref="EffectFamilyIndex"/> (build-plan Phase 30 step 30.4) from the
/// <c>EmbeddedResource</c> baked into this assembly at
/// <c>src/XlightsAI.Studio.Knowledge/SeedData/effect-family-index.json</c> -- same embedded-resource
/// convention as <see cref="EffectVisualCharacterCatalogLoader"/>/<see cref="MultiLayerTemplateCatalogLoader"/>:
/// committed, real-corpus-mined data, regenerated only by re-running the mining scan
/// (<c>EffectFamilyCorpusMiningTests</c> in <c>XlightsAI.Studio.Pipeline.Tests</c>) against the real
/// corpus, never a per-machine runtime artifact.
/// </summary>
public static class EffectFamilyIndexLoader
{
    private const string ResourceName = "XlightsAI.Studio.Knowledge.SeedData.effect-family-index.json";

    private static readonly Lazy<EffectFamilyIndex> Cached = new(LoadFromEmbeddedResource);

    /// <summary>The shippable, real-corpus-mined family index, parsed once and cached for the process lifetime.</summary>
    public static EffectFamilyIndex Load() => Cached.Value;

    private static EffectFamilyIndex LoadFromEmbeddedResource()
    {
        var assembly = typeof(EffectFamilyIndexLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found in assembly '{assembly.FullName}'. " +
                "Confirm it is still declared as an <EmbeddedResource> in XlightsAI.Studio.Knowledge.csproj.");

        var index = JsonSerializer.Deserialize<EffectFamilyIndex>(stream);
        return index ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' deserialized to null.");
    }
}
