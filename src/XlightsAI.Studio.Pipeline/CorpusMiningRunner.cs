using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.6-11; architecture §8b): the single reusable entry point a future phase (or
/// a developer re-running corpus mining after adding sequences) calls to go from a raw
/// <c>C:\xLights</c>-shaped corpus root all the way to persisted <see cref="PatternLibrary"/>,
/// <see cref="ExemplarIndex"/> and <see cref="CraftRules"/> artifacts — rather than each of those being
/// a one-off script. Each per-song analysis is independently try/caught: a single corrupt file or audio
/// decode failure excludes that one song (reported in <see cref="CorpusMiningResult.FailedSongs"/>)
/// rather than aborting the whole corpus run.
/// </summary>
public static class CorpusMiningRunner
{
    /// <summary>
    /// Per-song ceiling for <see cref="CorpusSongAnalyzer.AnalyzeAsync"/>. A single pathological song
    /// (corrupt/huge audio, a degenerate value that defeats an analysis algorithm's progress guarantees,
    /// or an external process that stops making progress) must exclude that one song, exactly like any
    /// other analysis failure, rather than hanging the entire corpus run with no way to tell -- from the
    /// outside -- that it is stuck rather than doing legitimately slow real work. A single song's full
    /// analysis chain (onset/tempo/beat-grid/energy/bar-fingerprint/section-boundary/section-label/
    /// phrase, each a real signal-processing pass over the whole song) measured at several real minutes
    /// per song on this machine -- consistent with <c>CorpusSongAnalyzerTests</c>'s own documented "on
    /// the order of an hour" estimate for the full 20-song corpus (~3 minutes/song average, with real
    /// variance above and below that). 10 minutes leaves generous headroom above that average for a
    /// legitimately slower song while still bounding a true hang to a knowable, logged duration.
    /// </summary>
    public static readonly TimeSpan DefaultPerSongTimeout = TimeSpan.FromMinutes(10);

    public static Task<CorpusMiningResult> RunAsync(string corpusRootDirectory, string knowledgeDirectory, CancellationToken ct) =>
        RunAsync(corpusRootDirectory, knowledgeDirectory, DefaultPerSongTimeout, progress: null, ct);

    /// <param name="perSongTimeout">
    /// Wall-clock ceiling for each song's <see cref="CorpusSongAnalyzer.AnalyzeAsync"/> call. A song that
    /// exceeds this is recorded in <see cref="CorpusMiningResult.FailedSongs"/> as a timeout (never
    /// silently dropped) and the run continues with the next song.
    /// </param>
    /// <param name="progress">
    /// Optional sink for a one-line, timestamped progress message before and after every song (and for
    /// the final library/index/rules build steps) -- the mechanism that lets a long real run be tailed
    /// from a log file instead of guessed at from console output alone.
    /// </param>
    public static async Task<CorpusMiningResult> RunAsync(
        string corpusRootDirectory,
        string knowledgeDirectory,
        TimeSpan perSongTimeout,
        Action<string>? progress,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);
        void Report(string message) => progress?.Invoke(message);

        var manifest = CorpusManifestBuilder.Build(corpusRootDirectory);
        await new JsonCorpusManifestStore(knowledgeDirectory).SaveAsync(manifest, ct).ConfigureAwait(false);
        Report($"Manifest built: {manifest.Entries.Count} entries, {manifest.ExcludedPaths.Count} excluded paths.");

        var analyzer = new CorpusSongAnalyzer(Path.Combine(knowledgeDirectory, "analysis-cache"));
        var analyses = new List<CorpusSongAnalysis>();
        var failures = new Dictionary<string, string>();

        var songIndex = 0;
        var totalSongs = manifest.Entries.Count;
        foreach (var entry in manifest.Entries)
        {
            songIndex++;
            ct.ThrowIfCancellationRequested();

            // A fresh, independently-timed-out linked token per song: a timeout on song N must exclude
            // only song N, never cancel the outer run or any later song.
            using var perSongCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perSongCts.CancelAfter(perSongTimeout);

            var startedUtc = DateTimeOffset.UtcNow;
            Report($"[{songIndex}/{totalSongs}] START '{entry.SongTitle}' at {startedUtc:O}");
            try
            {
                var analysis = await analyzer.AnalyzeAsync(entry, perSongCts.Token).ConfigureAwait(false);
                analyses.Add(analysis);
                Report($"[{songIndex}/{totalSongs}] DONE  '{entry.SongTitle}' in {(DateTimeOffset.UtcNow - startedUtc).TotalSeconds:F1}s");
            }
            catch (OperationCanceledException) when (perSongCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                var message = $"Timed out after {perSongTimeout.TotalMinutes:F1} minute(s) (song analysis made no progress within the bound).";
                failures[entry.SongTitle] = message;
                Report($"[{songIndex}/{totalSongs}] TIMEOUT '{entry.SongTitle}': {message}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures[entry.SongTitle] = $"{ex.GetType().Name}: {ex.Message}";
                Report($"[{songIndex}/{totalSongs}] FAILED '{entry.SongTitle}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        Report($"All songs processed: {analyses.Count} analyzed, {failures.Count} failed/timed out. Building PatternLibrary/ExemplarIndex/CraftRules...");

        var library = PatternLibraryBuilder.Build(analyses);
        var exemplarIndex = ExemplarIndexBuilder.Build(analyses);
        var craftRules = CraftRuleInducer.InduceAndVerify(analyses);

        var libraryPath = await new JsonPatternLibraryStore(knowledgeDirectory).SaveAsync(library, ct).ConfigureAwait(false);
        var indexPath = await new JsonExemplarIndexStore(knowledgeDirectory).SaveAsync(exemplarIndex, ct).ConfigureAwait(false);
        var rulesPath = await new JsonCraftRulesStore(knowledgeDirectory).SaveAsync(craftRules, ct).ConfigureAwait(false);
        Report($"Wrote '{libraryPath}', '{indexPath}', '{rulesPath}'.");

        return new CorpusMiningResult(manifest, analyses, failures, library, exemplarIndex, craftRules, libraryPath, indexPath, rulesPath);
    }
}

/// <summary>Everything one <see cref="CorpusMiningRunner.RunAsync"/> call produced.</summary>
public sealed record CorpusMiningResult(
    CorpusManifest Manifest,
    IReadOnlyList<CorpusSongAnalysis> Analyses,
    IReadOnlyDictionary<string, string> FailedSongs,
    PatternLibrary PatternLibrary,
    ExemplarIndex ExemplarIndex,
    CraftRules CraftRules,
    string PatternLibraryPath,
    string ExemplarIndexPath,
    string CraftRulesPath);
