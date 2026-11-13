using System.Diagnostics;
using XlightsAI.Studio.Pipeline;

// Build-plan Phase 25 Part C: a training-only one-off runner, parallel to tools/CorpusMiner but never
// touching the exemplar-eligible CorpusManifestBuilder.KnownResolutions allowlist or ExemplarIndex/
// MultiLayerTemplate pipelines -- see CorpusTrainingRunner's own doc comment. Drives
// CorpusTrainingRunner.RunAsync against an arbitrary external sequences folder, logging progress (with
// a running elapsed-time timer so a long real run never looks silently stuck) to both the console and a
// timestamped log file.
//
// Usage: dotnet run -c Release --project tools/CorpusTrainer -- <externalSequencesRoot> [knowledgeDirectory]
// Defaults: knowledgeDirectory=<externalSequencesRoot>/training-knowledge

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: CorpusTrainer <externalSequencesRoot> [knowledgeDirectory]");
    return 1;
}

var externalSequencesRoot = args[0];
var knowledgeDirectory = args.Length > 1 ? args[1] : Path.Combine(externalSequencesRoot, "training-knowledge");

Directory.CreateDirectory(knowledgeDirectory);
var logPath = Path.Combine(knowledgeDirectory, $"corpus-training-{DateTime.Now:yyyyMMdd-HHmmss}.log");
await using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

void Log(string message)
{
    var line = $"[{DateTimeOffset.Now:O}] {message}";
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

void LogLayerComplexityTier(string label, XlightsAI.Studio.Pipeline.LayerComplexityTierStats tier)
{
    if (tier.WindowCount == 0)
    {
        Log($"  {label}: no window reached this tier.");
        return;
    }

    Log($"  {label}: windows={tier.WindowCount}, duration (bars) median {tier.MedianDurationBars:0.0} / P90 {tier.P90DurationBars:0.0}");
    Log("    Section kinds: " + string.Join(", ", tier.SectionKindHistogram.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
}

Log($"External sequences root: {externalSequencesRoot}");
Log($"Knowledge directory: {knowledgeDirectory}");
Log($"Log file: {logPath}");

var stopwatch = Stopwatch.StartNew();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Log("Cancellation requested (Ctrl+C) -- stopping after the current song.");
    cts.Cancel();
};

CorpusTrainingResult result;
try
{
    result = await CorpusTrainingRunner.RunAsync(externalSequencesRoot, knowledgeDirectory, Log, cts.Token);
}
catch (Exception ex)
{
    Log($"FATAL: {ex}");
    return 1;
}

stopwatch.Stop();

Log("");
Log("==================== CORPUS TRAINING SUMMARY ====================");
Log($"Wall-clock time: {stopwatch.Elapsed:hh\\:mm\\:ss}");
Log($"Discovered: {result.Report.DiscoveredSequenceCount}; excluded: {result.Report.ExcludedSequenceCount}");
Log($"Analyzed successfully: {result.Report.AnalyzedSequenceCount}");
Log($"Failed: {result.Report.FailedSequenceCount}");
foreach (var reason in result.Report.FailureReasons)
{
    Log($"  FAILED {reason}");
}

var distribution = result.Report.CatchAllCoverageRatioDistribution;
Log("");
Log($"---- Catch-all group coverage-ratio distribution (songs with layout: {distribution.SongsWithLayoutCount}, samples: {distribution.GroupSampleCount}) ----");
if (distribution.GroupSampleCount > 0)
{
    Log($"  Min={distribution.Min:P1} Median={distribution.Median:P1} P90={distribution.P90:P1} Max={distribution.Max:P1}");
    foreach (var (bucket, count) in distribution.Histogram.OrderBy(kv => kv.Key))
    {
        Log($"    {bucket}: {count}");
    }
}
else
{
    Log("  No song in this run had a discoverable xlights_rgbeffects.xml alongside its .xsq.");
}

var coOccurrence = result.Report.WholeCanvasCoOccurrence;
Log("");
Log($"---- Whole-canvas co-occurrence (songs with a whole-canvas group: {coOccurrence.SongsWithWholeCanvasCount}, windows: {coOccurrence.WindowSampleCount}) ----");
if (coOccurrence.WindowSampleCount > 0)
{
    Log($"  Co-fires with Foundation: {coOccurrence.FoundationCoFireRatio:P1}; Subject: {coOccurrence.SubjectCoFireRatio:P1}; Accent: {coOccurrence.AccentCoFireRatio:P1}");
    Log($"  Duration (bars): median {coOccurrence.MedianDurationBars:0.0}, P90 {coOccurrence.P90DurationBars:0.0}");
    Log("  Section kinds: " + string.Join(", ", coOccurrence.SectionKindHistogram.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
    Log("  Energy tiers: " + string.Join(", ", coOccurrence.EnergyTierHistogram.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
}
else
{
    Log("  No song in this run had a group/model whose own pixel coverage classified it as whole-canvas.");
}

var layerComplexity = result.Report.LayerComplexity;
Log("");
Log("---- Layer-complexity distribution (3+/4+/5+ concurrent layers) ----");
LogLayerComplexityTier("3+", layerComplexity.ThreePlus);
LogLayerComplexityTier("4+", layerComplexity.FourPlus);
LogLayerComplexityTier("5+", layerComplexity.FivePlus);

var reuseRate = result.Report.ReuseRate;
Log("");
Log($"---- Reuse-rate (structurally similar passages recurring within one song; buckets: {reuseRate.TotalBucketCount}, windows: {reuseRate.TotalWindowCount}) ----");
if (reuseRate.TotalWindowCount > 0)
{
    Log($"  Reused buckets: {reuseRate.ReusedBucketCount}; repeated windows: {reuseRate.RepeatedWindowCount}; overall reuse ratio: {reuseRate.OverallReuseRatio:P1}");
    Log($"  Occurrences per reused bucket: median {reuseRate.MedianOccurrencesPerReusedBucket:0.0}, P90 {reuseRate.P90OccurrencesPerReusedBucket:0.0}");
    Log("  Section kinds of reused buckets: " + string.Join(", ", reuseRate.SectionKindHistogramOfReusedBuckets.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
}
else
{
    Log("  No song in this run had any usable phrase window.");
}

Log("");
Log($"Artifact written: {result.ReportPath}");
Log("===================================================================");

return 0;
