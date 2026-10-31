using System.Reflection;
using System.Text.Json;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Loads the shippable <see cref="MultiLayerTemplateCatalog"/> (build-plan Phase 10 §6b task 2/3) from
/// the <c>EmbeddedResource</c> baked into this assembly at
/// <c>src/XlightsAI.Studio.Knowledge/SeedData/multilayer-tree-matrix-templates.json</c> -- embedded
/// rather than copied as loose <c>Content</c> so it ships inside the DLL itself with no dependency on
/// the caller's working directory or on a normal <c>dotnet publish</c> preserving a sibling file next
/// to it; a plain <c>dotnet build</c>/<c>publish</c> of any project referencing this one already carries
/// it with no separate manual-copy step, satisfying the same "no release could forget this" discipline
/// <c>effectmetadata</c> itself depends on (there, the real xLights install directory; here, baked into
/// the assembly instead since this project owns and ships its own seed data).
/// </summary>
public static class MultiLayerTemplateCatalogLoader
{
    private const string ResourceName = "XlightsAI.Studio.Knowledge.SeedData.multilayer-tree-matrix-templates.json";

    private static readonly Lazy<MultiLayerTemplateCatalog> Cached = new(LoadFromEmbeddedResource);

    /// <summary>The shippable catalog, parsed once and cached for the process lifetime -- it is immutable seed data, never regenerated per call.</summary>
    public static MultiLayerTemplateCatalog Load() => Cached.Value;

    private static MultiLayerTemplateCatalog LoadFromEmbeddedResource()
    {
        var assembly = typeof(MultiLayerTemplateCatalogLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' was not found in assembly '{assembly.FullName}'. " +
                "Confirm it is still declared as an <EmbeddedResource> in XlightsAI.Studio.Knowledge.csproj.");

        var catalog = JsonSerializer.Deserialize<MultiLayerTemplateCatalog>(stream);
        return catalog ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' deserialized to null.");
    }
}
