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

    /// <summary>Build-plan Phase 20 §2b: the file name <c>XlightsAI.Studio.Pipeline.JsonMultiLayerTemplateCatalogStore</c> writes a real, corpus-mined catalog under -- read directly here (plain file + JSON, no project reference) since <c>Knowledge</c> cannot reference <c>Pipeline</c> (the dependency runs the other way).</summary>
    private const string MinedFileName = "multilayer-templates-mined.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<MultiLayerTemplateCatalog> Cached = new(LoadFromEmbeddedResource);

    /// <summary>The shippable, hand-curated seed catalog alone, parsed once and cached for the process lifetime -- it is immutable seed data, never regenerated per call.</summary>
    public static MultiLayerTemplateCatalog Load() => Cached.Value;

    /// <summary>
    /// Build-plan Phase 20 §2b: the same hand-curated seed catalog as <see cref="Load()"/>, merged with
    /// whatever real, corpus-mined templates a prior <c>CorpusMiningRunner</c> run wrote under
    /// <paramref name="knowledgeDirectory"/> (<c>multilayer-templates-mined.json</c>) -- additive only, so
    /// a knowledge directory with no mined file yet (or <paramref name="knowledgeDirectory"/> itself
    /// <see langword="null"/>) returns exactly the same 3-template seed catalog <see cref="Load()"/> does,
    /// never an error. Not cached (unlike <see cref="Load()"/>) since the mined file can change between
    /// corpus-mining runs within the same process.
    /// </summary>
    public static MultiLayerTemplateCatalog Load(string? knowledgeDirectory)
    {
        var seed = Load();
        if (knowledgeDirectory is null)
        {
            return seed;
        }

        var minedPath = System.IO.Path.Combine(knowledgeDirectory, MinedFileName);
        if (!File.Exists(minedPath))
        {
            return seed;
        }

        MultiLayerTemplateCatalog? mined;
        try
        {
            mined = JsonSerializer.Deserialize<MultiLayerTemplateCatalog>(File.ReadAllText(minedPath), JsonOptions);
        }
        catch (JsonException)
        {
            // A corrupt/partially-written mined file must never break a real generation -- fall back to
            // the seed catalog alone, same disclosed-fallback discipline as a missing CraftRules/
            // ExemplarIndex file in AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync.
            return seed;
        }

        if (mined is null || mined.Templates.Count == 0)
        {
            return seed;
        }

        var seedIds = new HashSet<string>(seed.Templates.Select(t => t.Id), StringComparer.Ordinal);
        var additiveMined = mined.Templates.Where(t => !seedIds.Contains(t.Id)).ToList();
        if (additiveMined.Count == 0)
        {
            return seed;
        }

        return new MultiLayerTemplateCatalog(
            Version: seed.Version + 1,
            GeneratedAtUtc: mined.GeneratedAtUtc > seed.GeneratedAtUtc ? mined.GeneratedAtUtc : seed.GeneratedAtUtc,
            Templates: seed.Templates.Concat(additiveMined).ToList());
    }

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
