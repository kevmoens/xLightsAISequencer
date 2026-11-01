using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 17 §2b: the real entry point for mining the live working tree together with one or
/// more historical git-archived seasons in a single combined run -- extracts each season commit (see
/// <see cref="SeasonalCorpusExtractor"/>), resolves it generically (see
/// <see cref="HistoricalCorpusManifestBuilder"/>, cross-referencing the live manifest's own
/// <c>KnownResolutions</c> provenance by song title so a historical copy of an already-known vendor
/// download is tagged the same way), deduplicates by content hash across all of them plus the live tree
/// (see <see cref="CorpusManifestMerger"/>), and runs the single combined manifest through the ordinary
/// <see cref="CorpusMiningRunner"/> pipeline exactly once.
/// </summary>
public static class SeasonalCorpusMiningOrchestrator
{
    public static async Task<CorpusMiningResult> RunAsync(
        string liveCorpusRootDirectory,
        string gitRepositoryDirectory,
        IReadOnlyList<string> seasonCommits,
        string scratchRootDirectory,
        string knowledgeDirectory,
        TimeSpan perSongTimeout,
        Action<string>? progress,
        CancellationToken ct,
        IExemplarDescriptionClient? descriptionClient = null,
        int maxSongConcurrency = CorpusMiningRunner.DefaultMaxSongConcurrency,
        IReadOnlyList<string>? excludedProjectRootDirectories = null,
        int favoriteSongWeightMultiplier = CraftRuleInducer.DefaultFavoriteSongWeightMultiplier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(liveCorpusRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitRepositoryDirectory);
        ArgumentNullException.ThrowIfNull(seasonCommits);
        ArgumentException.ThrowIfNullOrWhiteSpace(scratchRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);
        void Report(string message) => progress?.Invoke(message);

        var liveManifest = CorpusManifestBuilder.Build(liveCorpusRootDirectory, excludedProjectRootDirectories);
        Report($"Live manifest: {liveManifest.Entries.Count} entries, {liveManifest.ExcludedPaths.Count} excluded.");

        var knownProvenanceByTitle = liveManifest.Entries
            .GroupBy(e => e.SongTitle, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Provenance, StringComparer.OrdinalIgnoreCase);

        var allManifests = new List<CorpusManifest> { liveManifest };
        foreach (var commit in seasonCommits)
        {
            Report($"Extracting season commit '{commit}'...");
            var scratchDirectory = await SeasonalCorpusExtractor
                .ExtractAsync(gitRepositoryDirectory, commit, scratchRootDirectory, ct)
                .ConfigureAwait(false);

            var seasonManifest = HistoricalCorpusManifestBuilder.Build(scratchDirectory, commit, knownProvenanceByTitle);
            Report($"Season '{commit}': {seasonManifest.Entries.Count} entries, {seasonManifest.ExcludedPaths.Count} excluded.");
            allManifests.Add(seasonManifest);
        }

        var merged = CorpusManifestMerger.MergeDeduplicatingByContentHash(allManifests);
        Report($"Merged manifest (deduplicated by content hash): {merged.Entries.Count} entries across {allManifests.Count} source manifest(s).");

        return await CorpusMiningRunner
            .RunAsync(
                merged, knowledgeDirectory, perSongTimeout, progress, ct, descriptionClient, maxSongConcurrency,
                favoriteSongWeightMultiplier)
            .ConfigureAwait(false);
    }
}
