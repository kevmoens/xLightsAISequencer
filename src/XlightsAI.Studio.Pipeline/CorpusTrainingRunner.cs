using System.Diagnostics;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part C: the training-only entry point, parallel to (but never feeding)
/// <see cref="CorpusMiningRunner"/>'s exemplar-eligible pipeline. Discovers an arbitrary external
/// sequence folder via <see cref="ExternalTrainingManifestBuilder"/> (no <see cref="CorpusManifestBuilder.KnownResolutions"/>
/// allowlist membership required), runs the real <see cref="CorpusSongAnalyzer"/> over every discovered
/// sequence exactly like a normal corpus run, and writes only an aggregate <see cref="CorpusTrainingReport"/>
/// -- never an <c>ExemplarIndex</c>/<c>MultiLayerTemplate</c> artifact, and never any raw effect/model/
/// setting string.
/// </summary>
public static class CorpusTrainingRunner
{
    /// <summary>Coverage-ratio histogram bucket count/width (Part A's distribution is what calibrated <c>BandEnergyGroupRoleMapper.WholeCanvasCoverageThreshold</c> -- see that constant's own doc comment for the real measured numbers).</summary>
    private const int HistogramBucketCount = 10;

    public static async Task<CorpusTrainingResult> RunAsync(
        string externalSequencesRoot,
        string knowledgeDirectory,
        Action<string>? progress,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalSequencesRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeDirectory);
        void Report(string message) => progress?.Invoke(message);

        var manifest = ExternalTrainingManifestBuilder.Build(externalSequencesRoot);
        Report($"Discovered {manifest.Entries.Count} external sequence(s) ({manifest.ExcludedPaths.Count} excluded).");

        var analyzer = new CorpusSongAnalyzer(Path.Combine(knowledgeDirectory, "training-analysis-cache"));
        var coverageRatios = new List<double>();
        var songsWithLayout = 0;
        var coOccurrenceSamples = new List<WholeCanvasCoOccurrenceSample>();
        var songsWithWholeCanvas = 0;
        var layerComplexitySamples = new List<LayerComplexitySample>();
        var reuseRateBuckets = new List<ReuseRateBucketSample>();
        var failures = new List<string>();
        var analyzedCount = 0;

        // A real per-song analysis (onset/tempo/beat-grid/energy/pitch/structure) can run minutes on a
        // real external sequence -- the same real cost CorpusMiningRunner's own progress log already
        // discloses -- so every line below reports total elapsed wall-clock time, not just per-song
        // duration, so a long real run can be tailed and never looks silently stuck.
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < manifest.Entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = manifest.Entries[i];
            Report($"[{i + 1}/{manifest.Entries.Count}] Analyzing '{entry.SongTitle}'... (elapsed {stopwatch.Elapsed:hh\\:mm\\:ss})");

            try
            {
                var analysis = await analyzer.AnalyzeAsync(entry, ct).ConfigureAwait(false);
                analyzedCount++;
                Report($"[{i + 1}/{manifest.Entries.Count}] DONE  '{entry.SongTitle}' (elapsed {stopwatch.Elapsed:hh\\:mm\\:ss})");

                layerComplexitySamples.AddRange(LayerComplexityMiner.Mine(analysis));
                reuseRateBuckets.AddRange(ReuseRateMiner.Mine(analysis));

                var layout = await TryImportLayoutAsync(entry, ct).ConfigureAwait(false);
                if (layout is not null)
                {
                    var ratios = ComputeGroupCoverageRatios(layout);
                    if (ratios.Count > 0)
                    {
                        songsWithLayout++;
                        coverageRatios.AddRange(ratios);
                    }

                    var samples = WholeCanvasCoOccurrenceMiner.Mine(analysis, layout);
                    if (samples.Count > 0)
                    {
                        songsWithWholeCanvas++;
                        coOccurrenceSamples.AddRange(samples);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add($"{entry.SongTitle}: {ex.GetType().Name}: {ex.Message}");
                Report($"[{i + 1}/{manifest.Entries.Count}] FAILED '{entry.SongTitle}': {ex.GetType().Name}: {ex.Message} (elapsed {stopwatch.Elapsed:hh\\:mm\\:ss})");
            }
        }

        var distribution = BuildDistribution(coverageRatios, songsWithLayout);
        var coOccurrenceStats = BuildCoOccurrenceStats(coOccurrenceSamples, songsWithWholeCanvas);
        var layerComplexityStats = BuildLayerComplexityStats(layerComplexitySamples);
        var reuseRateStats = BuildReuseRateStats(reuseRateBuckets);
        var report = new CorpusTrainingReport(
            DateTimeOffset.UtcNow,
            manifest.RootDirectory,
            manifest.Entries.Count,
            manifest.ExcludedPaths.Count,
            analyzedCount,
            failures.Count,
            failures,
            distribution,
            coOccurrenceStats,
            layerComplexityStats,
            reuseRateStats);

        var reportPath = await new JsonCorpusTrainingReportStore(knowledgeDirectory).SaveAsync(report, ct).ConfigureAwait(false);
        Report($"Wrote '{reportPath}' (total elapsed {stopwatch.Elapsed:hh\\:mm\\:ss}).");

        return new CorpusTrainingResult(manifest, report, reportPath);
    }

    /// <summary>
    /// Best-effort: unlike <see cref="CorpusSongAnalyzer"/> (which never needs a layout at all), an
    /// external training sequence's show directory may or may not carry its own real
    /// <c>xlights_rgbeffects.xml</c> alongside the <c>.xsq</c>. Returns <see langword="null"/> when no
    /// layout is found or fails to parse, rather than guessing -- that song simply contributes no
    /// samples to either the coverage-ratio distribution or the whole-canvas co-occurrence mining below.
    /// </summary>
    private static async Task<DisplayGraph?> TryImportLayoutAsync(CorpusEntry entry, CancellationToken ct)
    {
        var showDirectory = Path.GetDirectoryName(entry.SequencePath);
        if (showDirectory is null || !File.Exists(Path.Combine(showDirectory, ShowDirectoryFileNames.RgbEffects)))
        {
            return null;
        }

        try
        {
            return await new ShowLayoutImporter().ImportReadOnlyAsync(showDirectory, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Pure group-coverage-ratio computation over an already-imported layout. Returns an empty list when the layout carries no addressable pixels or no groups at all, rather than guessing.</summary>
    private static IReadOnlyList<double> ComputeGroupCoverageRatios(DisplayGraph layout)
    {
        var totalPixels = layout.Models.Sum(m => (long)Math.Max(0, m.NodeCount));
        if (totalPixels <= 0 || layout.Groups.Count == 0)
        {
            return [];
        }

        var modelPixelsByName = layout.Models.ToDictionary(m => m.XlightsName, m => (long)Math.Max(0, m.NodeCount), StringComparer.Ordinal);
        var ratios = new List<double>();
        foreach (var group in layout.Groups)
        {
            var memberPixels = group.Members
                .Where(m => m.Kind == DisplayGroupMemberKind.Model)
                .Sum(m => modelPixelsByName.GetValueOrDefault(m.RawName, 0));
            ratios.Add((double)memberPixels / totalPixels);
        }

        return ratios;
    }

    /// <summary>Build-plan Phase 25 Part B.4: aggregates every sampled <see cref="WholeCanvasCoOccurrenceSample"/> across the whole training run into one <see cref="WholeCanvasCoOccurrenceStats"/>.</summary>
    private static WholeCanvasCoOccurrenceStats BuildCoOccurrenceStats(IReadOnlyList<WholeCanvasCoOccurrenceSample> samples, int songsWithWholeCanvas)
    {
        if (samples.Count == 0)
        {
            return new WholeCanvasCoOccurrenceStats(0, songsWithWholeCanvas, null, null, null, new Dictionary<string, int>(), new Dictionary<string, int>(), null, null);
        }

        double Ratio(Func<WholeCanvasCoOccurrenceSample, bool> predicate) => samples.Count(predicate) / (double)samples.Count;

        var sectionHistogram = samples
            .GroupBy(s => s.SectionKind?.ToString() ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var energyTierHistogram = samples
            .Where(s => s.EnergyPercentile is not null)
            .GroupBy(s => EnergyTier(s.EnergyPercentile!.Value))
            .ToDictionary(g => g.Key, g => g.Count());

        var durations = samples.Where(s => s.DurationBars is not null).Select(s => s.DurationBars!.Value).OrderBy(d => d).ToList();

        return new WholeCanvasCoOccurrenceStats(
            samples.Count,
            songsWithWholeCanvas,
            Ratio(s => s.CoFiresWithFoundation),
            Ratio(s => s.CoFiresWithSubject),
            Ratio(s => s.CoFiresWithAccent),
            sectionHistogram,
            energyTierHistogram,
            durations.Count > 0 ? Percentile(durations, 0.5) : null,
            durations.Count > 0 ? Percentile(durations, 0.9) : null);
    }

    private static string EnergyTier(double percentile) => percentile switch
    {
        < 1.0 / 3 => "Low",
        < 2.0 / 3 => "Mid",
        _ => "High",
    };

    /// <summary>Build-plan Phase 25 Part C §3 bullet 3: aggregates every sampled <see cref="LayerComplexitySample"/> into the 3+/4+/5+ report tiers.</summary>
    private static LayerComplexityStats BuildLayerComplexityStats(IReadOnlyList<LayerComplexitySample> samples) => new(
        BuildLayerComplexityTier(samples, minPeak: 3),
        BuildLayerComplexityTier(samples, minPeak: 4),
        BuildLayerComplexityTier(samples, minPeak: 5));

    private static LayerComplexityTierStats BuildLayerComplexityTier(IReadOnlyList<LayerComplexitySample> samples, int minPeak)
    {
        var tierSamples = samples.Where(s => s.PeakConcurrentLayerCount >= minPeak).ToList();
        if (tierSamples.Count == 0)
        {
            return new LayerComplexityTierStats(0, new Dictionary<string, int>(), null, null);
        }

        var sectionHistogram = tierSamples
            .GroupBy(s => s.SectionKind?.ToString() ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var durations = tierSamples.Where(s => s.DurationBars is not null).Select(s => s.DurationBars!.Value).OrderBy(d => d).ToList();

        return new LayerComplexityTierStats(
            tierSamples.Count,
            sectionHistogram,
            durations.Count > 0 ? Percentile(durations, 0.5) : null,
            durations.Count > 0 ? Percentile(durations, 0.9) : null);
    }

    /// <summary>Build-plan Phase 25 Part C §3 bullet 4: aggregates every sampled <see cref="ReuseRateBucketSample"/> across the whole training run into one <see cref="ReuseRateStats"/>.</summary>
    private static ReuseRateStats BuildReuseRateStats(IReadOnlyList<ReuseRateBucketSample> buckets)
    {
        if (buckets.Count == 0)
        {
            return new ReuseRateStats(0, 0, 0, 0, null, null, null, new Dictionary<string, int>());
        }

        var totalWindowCount = buckets.Sum(b => b.OccurrenceCount);
        var reused = buckets.Where(b => b.OccurrenceCount >= 2).ToList();
        var repeatedWindowCount = reused.Sum(b => b.OccurrenceCount - 1);

        var sectionHistogram = reused
            .GroupBy(b => b.SectionKind?.ToString() ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var occurrenceCounts = reused.Select(b => (double)b.OccurrenceCount).OrderBy(c => c).ToList();

        return new ReuseRateStats(
            buckets.Count,
            totalWindowCount,
            reused.Count,
            repeatedWindowCount,
            totalWindowCount > 0 ? repeatedWindowCount / (double)totalWindowCount : null,
            occurrenceCounts.Count > 0 ? Percentile(occurrenceCounts, 0.5) : null,
            occurrenceCounts.Count > 0 ? Percentile(occurrenceCounts, 0.9) : null,
            sectionHistogram);
    }

    private static CoverageRatioDistribution BuildDistribution(IReadOnlyList<double> ratios, int songsWithLayout)
    {
        var histogram = new Dictionary<string, int>();
        for (var bucket = 0; bucket < HistogramBucketCount; bucket++)
        {
            histogram[BucketLabel(bucket)] = 0;
        }

        foreach (var ratio in ratios)
        {
            var bucket = Math.Clamp((int)(ratio * HistogramBucketCount), 0, HistogramBucketCount - 1);
            histogram[BucketLabel(bucket)]++;
        }

        if (ratios.Count == 0)
        {
            return new CoverageRatioDistribution(0, songsWithLayout, null, null, null, null, histogram);
        }

        var sorted = ratios.OrderBy(r => r).ToList();
        return new CoverageRatioDistribution(
            sorted.Count,
            songsWithLayout,
            sorted[0],
            sorted[^1],
            Percentile(sorted, 0.5),
            Percentile(sorted, 0.9),
            histogram);
    }

    private static string BucketLabel(int bucket)
    {
        var lower = bucket / (double)HistogramBucketCount;
        var upper = (bucket + 1) / (double)HistogramBucketCount;
        return bucket == HistogramBucketCount - 1 ? $"{lower:0.0}-{upper:0.0}+" : $"{lower:0.0}-{upper:0.0}";
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        var index = (int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[index];
    }
}

/// <summary>Everything one <see cref="CorpusTrainingRunner.RunAsync"/> call produced.</summary>
public sealed record CorpusTrainingResult(CorpusManifest Manifest, CorpusTrainingReport Report, string ReportPath);
