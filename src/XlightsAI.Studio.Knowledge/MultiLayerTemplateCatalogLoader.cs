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

    /// <summary>Build-plan Phase 27 (2026-11-14): the file name <c>tools/CorpusMiner</c> writes real, single-song "showcase" stacks (below the mined file's own promotion bar) under -- additive to, never a replacement for, either the seed or mined catalog.</summary>
    private const string ShowcaseFileName = "multilayer-templates-showcase.json";

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
        var catalog = Load();
        if (knowledgeDirectory is null)
        {
            return catalog;
        }

        catalog = MergeAdditiveFile(catalog, System.IO.Path.Combine(knowledgeDirectory, MinedFileName));
        catalog = MergeAdditiveFile(catalog, System.IO.Path.Combine(knowledgeDirectory, ShowcaseFileName));
        return catalog;
    }

    /// <summary>
    /// Build-plan Phase 27 (2026-11-14): factored out of the original single-file <c>Load(string?)</c> body
    /// so both the mined file and the new showcase file (see <see cref="ShowcaseFileName"/>) share the
    /// exact same additive, never-fatal-on-missing/corrupt merge discipline.
    /// </summary>
    private static MultiLayerTemplateCatalog MergeAdditiveFile(MultiLayerTemplateCatalog baseCatalog, string path)
    {
        if (!File.Exists(path))
        {
            return baseCatalog;
        }

        MultiLayerTemplateCatalog? additional;
        try
        {
            additional = JsonSerializer.Deserialize<MultiLayerTemplateCatalog>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException)
        {
            // A corrupt/partially-written file must never break a real generation -- fall back to the
            // base catalog alone, same disclosed-fallback discipline as a missing CraftRules/ExemplarIndex
            // file in AiChoreographyWholeSongGenerator.LoadCorpusKnowledgeAsync.
            return baseCatalog;
        }

        if (additional is null || additional.Templates.Count == 0)
        {
            return baseCatalog;
        }

        var existingIds = new HashSet<string>(baseCatalog.Templates.Select(t => t.Id), StringComparer.Ordinal);
        var additive = additional.Templates.Where(t => !existingIds.Contains(t.Id)).ToList();
        if (additive.Count == 0)
        {
            return baseCatalog;
        }

        return new MultiLayerTemplateCatalog(
            Version: baseCatalog.Version + 1,
            GeneratedAtUtc: additional.GeneratedAtUtc > baseCatalog.GeneratedAtUtc ? additional.GeneratedAtUtc : baseCatalog.GeneratedAtUtc,
            Templates: baseCatalog.Templates.Concat(additive).ToList());
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
