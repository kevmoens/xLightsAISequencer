using System.Collections.Concurrent;
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

    /// <summary>
    /// Quality/throughput follow-up alongside build-plan Phase 16: how many songs' own
    /// <see cref="CorpusSongAnalyzer.AnalyzeAsync"/> calls run concurrently by default. Each song's
    /// analysis is a real signal-processing pipeline (onset/tempo/beat-grid/energy/pitch-tracking/
    /// bar-fingerprint/section/phrase/band-energy/vocal-activity, several real minutes) that is CPU/disk-
    /// bound, not network-bound -- running every song one at a time (the pre-Phase-16 behavior) left most
    /// of a multi-core machine idle for nearly the whole run. <c>0</c> (the default) means "use every
    /// available logical processor" (<see cref="Environment.ProcessorCount"/>) -- confirmed against a real
    /// 20-song run that a fixed low cap (4) left a 20-core machine at ~15% utilization, so defaulting to
    /// the hardware's own real concurrency is the better default than guessing a fixed number. A caller
    /// can still pass a smaller positive value to deliberately throttle a run sharing the machine with
    /// other work.
    /// </summary>
    public const int DefaultMaxSongConcurrency = 0;

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
    /// <param name="descriptionClient">
    /// Build-plan Phase 16 §2b: when supplied, every <see cref="ExemplarEntry"/>
    /// <see cref="ExemplarIndexBuilder.Build"/> produces is enriched with a real, model-written
    /// <see cref="ExemplarEntry.Description"/> via <see cref="ExemplarDescriptionEnricher"/> before the
    /// index is persisted. Defaults to <see langword="null"/> so every pre-existing caller (and any run
    /// where no description-generating role is configured) keeps compiling and behaving exactly as
    /// before -- descriptions are an enhancement, never a requirement for a corpus-mining run to succeed.
    /// </param>
    /// <param name="maxSongConcurrency">See <see cref="DefaultMaxSongConcurrency"/> -- <c>0</c> means "every available processor"; a positive value is clamped to <c>[1, Environment.ProcessorCount]</c> so it can never over-subscribe this machine.</param>
    /// <param name="excludedProjectRootDirectories">Build-plan Phase 17 §2a: forwarded as-is to <see cref="CorpusManifestBuilder.Build"/>'s own parameter of the same name. Defaults to <see langword="null"/> so every pre-existing caller keeps compiling and behaving exactly as before.</param>
    /// <param name="favoriteSongWeightMultiplier">Build-plan Phase 17 §2f: forwarded to <see cref="CraftRuleInducer.InduceAndVerify"/>'s own parameter of the same name.</param>
    public static async Task<CorpusMiningResult> RunAsync(
        string corpusRootDirectory,
        string knowledgeDirectory,
        TimeSpan perSongTimeout,
        Action<string>? progress,
        CancellationToken ct,
        IExemplarDescriptionClient? descriptionClient = null,
        int maxSongConcurrency = DefaultMaxSongConcurrency,
        IReadOnlyList<string>? excludedProjectRootDirectories = null,
        int favoriteSongWeightMultiplier = CraftRuleInducer.DefaultFavoriteSongWeightMultiplier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);

        var manifest = CorpusManifestBuilder.Build(corpusRootDirectory, excludedProjectRootDirectories);
        return await RunAsync(
            manifest, knowledgeDirectory, perSongTimeout, progress, ct, descriptionClient, maxSongConcurrency,
            favoriteSongWeightMultiplier).ConfigureAwait(false);
    }

    /// <summary>
    /// Build-plan Phase 17 §2b: the same analysis/library/index/rules pipeline as the directory-based
    /// <see cref="RunAsync(string,string,TimeSpan,Action{string}?,CancellationToken,IExemplarDescriptionClient?,int,IReadOnlyList{string}?,int)"/>
    /// overload, but starting from an already-built <see cref="CorpusManifest"/> instead of building one
    /// itself from a single live directory -- the entry point a seasonal corpus-extraction caller uses
    /// once it has already merged a live working-tree manifest with one or more historical, git-archived
    /// season manifests (see <see cref="CorpusManifestMerger"/>) into one combined manifest spanning
    /// absolute paths under several different scratch directories at once (every <see cref="CorpusEntry"/>
    /// carries its own absolute <see cref="CorpusEntry.SequencePath"/>/<see cref="CorpusEntry.AudioPath"/>,
    /// so nothing downstream needs a single common root directory).
    /// </summary>
    public static async Task<CorpusMiningResult> RunAsync(
        CorpusManifest manifest,
        string knowledgeDirectory,
        TimeSpan perSongTimeout,
        Action<string>? progress,
        CancellationToken ct,
        IExemplarDescriptionClient? descriptionClient = null,
        int maxSongConcurrency = DefaultMaxSongConcurrency,
        int favoriteSongWeightMultiplier = CraftRuleInducer.DefaultFavoriteSongWeightMultiplier)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);
        void Report(string message) => progress?.Invoke(message);

        await new JsonCorpusManifestStore(knowledgeDirectory).SaveAsync(manifest, ct).ConfigureAwait(false);
        Report($"Manifest built: {manifest.Entries.Count} entries, {manifest.ExcludedPaths.Count} excluded paths.");

        var analyzer = new CorpusSongAnalyzer(Path.Combine(knowledgeDirectory, "analysis-cache"));
        var totalSongs = manifest.Entries.Count;
        var analysisSlots = new CorpusSongAnalysis?[totalSongs];
        var failures = new ConcurrentDictionary<string, string>();

        // Quality/throughput follow-up: songs analyze concurrently (bounded by maxSongConcurrency, never
        // exceeding this machine's own core count) instead of strictly one at a time -- see
        // DefaultMaxSongConcurrency's own doc comment for why. Each song still gets its own independently
        // timed-out linked token and independent try/catch (a timeout/failure on song N excludes only
        // song N), exactly as the prior sequential version did; only the scheduling changed.
        var effectiveConcurrency = maxSongConcurrency <= 0
            ? Math.Max(1, Environment.ProcessorCount)
            : Math.Clamp(maxSongConcurrency, 1, Math.Max(1, Environment.ProcessorCount));
        using var gate = new SemaphoreSlim(effectiveConcurrency);

        var songTasks = manifest.Entries.Select(async (entry, i) =>
        {
            // Build-plan Phase 16/17 real-run finding: an ExternalReference entry (the Johnson Family
            // reference show discussed throughout the architecture doc) is already excluded from every
            // downstream consumer (PatternLibraryBuilder/ExemplarIndexBuilder/CraftRuleInducer all filter
            // it out), so its full real analysis result is never actually used for anything -- yet it is
            // also this corpus's single most pathological track for CorpusSongAnalyzer's own disclosed
            // zero-chroma-melody gap (see that type's doc comment), confirmed against a real run to take
            // 20+ minutes and still need its own per-song timeout to end it. Skipping analysis entirely
            // for this provenance avoids wasting that time on a result nothing downstream reads, rather
            // than fixing cancellation-responsiveness deep inside BandEnergyAnalyzer's tight per-frame loop.
            if (entry.Provenance == CorpusProvenanceTag.ExternalReference)
            {
                Report($"[{i + 1}/{totalSongs}] SKIPPED '{entry.SongTitle}' (ExternalReference provenance -- excluded from every downstream consumer, never analyzed).");
                return;
            }

            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();

                using var perSongCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                perSongCts.CancelAfter(perSongTimeout);

                var startedUtc = DateTimeOffset.UtcNow;
                Report($"[{i + 1}/{totalSongs}] START '{entry.SongTitle}' at {startedUtc:O}");
                try
                {
                    var analysis = await analyzer.AnalyzeAsync(entry, perSongCts.Token).ConfigureAwait(false);
                    analysisSlots[i] = analysis;
                    Report($"[{i + 1}/{totalSongs}] DONE  '{entry.SongTitle}' in {(DateTimeOffset.UtcNow - startedUtc).TotalSeconds:F1}s");
                }
                catch (OperationCanceledException) when (perSongCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    var message = $"Timed out after {perSongTimeout.TotalMinutes:F1} minute(s) (song analysis made no progress within the bound).";
                    failures[entry.SongTitle] = message;
                    Report($"[{i + 1}/{totalSongs}] TIMEOUT '{entry.SongTitle}': {message}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures[entry.SongTitle] = $"{ex.GetType().Name}: {ex.Message}";
                    Report($"[{i + 1}/{totalSongs}] FAILED '{entry.SongTitle}': {ex.GetType().Name}: {ex.Message}");
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(songTasks).ConfigureAwait(false);

        // Manifest order preserved (not completion order) so a re-run against unchanged input is
        // byte-for-byte reproducible downstream -- same reasoning PatternLibraryBuilder/ExemplarIndexBuilder's
        // own determinism already assumes.
        var analyses = analysisSlots.Where(a => a is not null).Select(a => a!).ToList();

        Report($"All songs processed: {analyses.Count} analyzed, {failures.Count} failed/timed out. Building PatternLibrary/ExemplarIndex/CraftRules...");

        var library = PatternLibraryBuilder.Build(analyses);
        var exemplarIndex = ExemplarIndexBuilder.Build(analyses);
        var craftRules = CraftRuleInducer.InduceAndVerify(analyses, favoriteSongWeightMultiplier: favoriteSongWeightMultiplier);

        if (descriptionClient is not null && exemplarIndex.Entries.Count > 0)
        {
            Report($"Generating real exemplar descriptions for {exemplarIndex.Entries.Count} entries (max {ExemplarDescriptionEnricher.DefaultMaxConcurrency} concurrent calls)...");
            exemplarIndex = await ExemplarDescriptionEnricher
                .EnrichAsync(exemplarIndex, descriptionClient, ExemplarDescriptionEnricher.DefaultMaxConcurrency, progress, ct)
                .ConfigureAwait(false);
        }

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
