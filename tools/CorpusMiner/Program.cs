using System.Diagnostics;
using XlightsAI.Studio.Pipeline;

// PHASE-2b one-off runner: drives CorpusMiningRunner end to end against a real C:\xLights-shaped
// corpus, logging every song's start/end/failure to BOTH the console and a timestamped log file (so a
// long run can be tailed/diagnosed without guessing whether it is stuck), with a bounded per-song
// timeout so one pathological song can never hang the whole run again (see
// XlightsAI.Studio.Audio.FfmpegProcessTranscoder's fixed stdout-drain deadlock and
// CorpusMiningRunner.RunAsync's new per-song timeout).
//
// Usage: dotnet run -c Release --project tools/CorpusMiner -- [corpusRoot] [knowledgeDirectory] [perSongTimeoutMinutes]
// Defaults: corpusRoot=C:\xLights, knowledgeDirectory=C:\xLights\knowledge, perSongTimeoutMinutes=3

var corpusRoot = args.Length > 0 ? args[0] : @"C:\xLights";
var knowledgeDirectory = args.Length > 1 ? args[1] : Path.Combine(corpusRoot, "knowledge");
var perSongTimeoutMinutes = args.Length > 2 ? double.Parse(args[2]) : CorpusMiningRunner.DefaultPerSongTimeout.TotalMinutes;
var perSongTimeout = TimeSpan.FromMinutes(perSongTimeoutMinutes);

Directory.CreateDirectory(knowledgeDirectory);
var logPath = Path.Combine(knowledgeDirectory, $"corpus-mining-{DateTime.Now:yyyyMMdd-HHmmss}.log");
await using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

void Log(string message)
{
    var line = $"[{DateTimeOffset.Now:O}] {message}";
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

Log($"Corpus root: {corpusRoot}");
Log($"Knowledge directory: {knowledgeDirectory}");
Log($"Per-song timeout: {perSongTimeout.TotalMinutes:F1} minute(s)");
Log($"Log file: {logPath}");

var stopwatch = Stopwatch.StartNew();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Log("Cancellation requested (Ctrl+C) -- stopping after the current song.");
    cts.Cancel();
};

CorpusMiningResult result;
try
{
    result = await CorpusMiningRunner.RunAsync(corpusRoot, knowledgeDirectory, perSongTimeout, Log, cts.Token);
}
catch (Exception ex)
{
    Log($"FATAL: {ex}");
    return 1;
}

stopwatch.Stop();

Log("");
Log("==================== CORPUS MINING SUMMARY ====================");
Log($"Wall-clock time: {stopwatch.Elapsed:hh\\:mm\\:ss}");
Log($"Manifest entries: {result.Manifest.Entries.Count}; excluded paths: {result.Manifest.ExcludedPaths.Count}");
Log($"Analyzed successfully: {result.Analyses.Count}");
Log($"Failed/timed out: {result.FailedSongs.Count}");
foreach (var (song, reason) in result.FailedSongs)
{
    Log($"  FAILED '{song}': {reason}");
}

Log("");
Log("---- Manifest exclusions (not mined at all) ----");
foreach (var (path, reason) in result.Manifest.ExcludedPaths)
{
    Log($"  {path}: {reason}");
}

Log("");
Log($"---- PatternLibrary (songs={result.PatternLibrary.SongCount}, effects={result.PatternLibrary.TotalEffectCount}) ----");
foreach (var (effect, count) in result.PatternLibrary.EffectFrequency.OrderByDescending(kv => kv.Value))
{
    Log($"  {effect,-24} {count}");
}
Log($"  Mode A (timing-track-bound): {result.PatternLibrary.ModeACount}");
Log($"  Mode B (discrete per-event): {result.PatternLibrary.ModeBCount}");
Log($"  Mode A ratio: {result.PatternLibrary.ModeARatio:P1}");

Log("");
Log($"---- ExemplarIndex ----");
Log($"  Entries: {result.ExemplarIndex.Entries.Count}");

Log("");
Log($"---- CraftRules (threshold: confidence>={result.CraftRules.MinConfidenceThreshold:P0}, songs>={result.CraftRules.MinSongCountThreshold}) ----");
Log($"  Promoted: {result.CraftRules.Promoted.Count}");
foreach (var rule in result.CraftRules.Promoted)
{
    Log($"    [{rule.Id}] confidence={rule.Confidence:P1} ({rule.TrueInstanceCount}/{rule.ApplicableInstanceCount}), songs={rule.SongCount}");
    Log($"      {rule.Statement}");
}
Log($"  Rejected candidates: {result.CraftRules.RejectedCandidates.Count}");
foreach (var rule in result.CraftRules.RejectedCandidates)
{
    Log($"    [{rule.Id}] confidence={rule.Confidence:P1} ({rule.TrueInstanceCount}/{rule.ApplicableInstanceCount}), songs={rule.SongCount} -- NOT promoted");
}

Log("");
Log($"Artifacts written:");
Log($"  {result.PatternLibraryPath}");
Log($"  {result.ExemplarIndexPath}");
Log($"  {result.CraftRulesPath}");
Log("=================================================================");

return 0;
