using System.Diagnostics;
using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Pipeline;
using XlightsAI.Studio.Projects;

// PHASE-2b one-off runner: drives CorpusMiningRunner end to end against a real C:\xLights-shaped
// corpus, logging every song's start/end/failure to BOTH the console and a timestamped log file (so a
// long run can be tailed/diagnosed without guessing whether it is stuck), with a bounded per-song
// timeout so one pathological song can never hang the whole run again (see
// XlightsAI.Studio.Audio.FfmpegProcessTranscoder's fixed stdout-drain deadlock and
// CorpusMiningRunner.RunAsync's new per-song timeout).
//
// Usage: dotnet run -c Release --project tools/CorpusMiner -- [corpusRoot] [knowledgeDirectory] [perSongTimeoutMinutes] [maxSongConcurrency] [seasonCommits]
// Defaults: corpusRoot=C:\xLights, knowledgeDirectory=C:\xLights\knowledge, perSongTimeoutMinutes=3,
// maxSongConcurrency=0 (every available logical processor -- see CorpusMiningRunner.DefaultMaxSongConcurrency)
//
// Build-plan Phase 16 §2b: if the Director role is configured under the real app's own
// %AppData%\XlightsAI.Studio\ai-providers/credentials (the same files the Shell UI reads/writes), this
// run also generates a real exemplar description per entry via that role's configured client. Missing/
// unconfigured credentials are reported and otherwise non-fatal -- mining still completes without
// descriptions, same as the Shell's own "Run corpus mining" action.
//
// Build-plan Phase 17 §2b: an optional 5th argument, a comma-separated list of git commit hashes (one
// per season, e.g. "8932a18,372e9b9,2dabbea,3c70b9b,da8cd81,749d18c,cfc240e,964584a,48c2dea,49ef88d"),
// switches to SeasonalCorpusMiningOrchestrator.RunAsync -- extracting and mining each season commit's
// historical tree alongside the live corpusRoot, deduplicated by content hash, as one combined run --
// instead of mining corpusRoot alone. Each season is extracted (via `git archive`/`tar`, read-only
// against the repository, no checkout) into Path.Combine(knowledgeDirectory, "season-scratch").

var corpusRoot = args.Length > 0 ? args[0] : @"C:\xLights";
var knowledgeDirectory = args.Length > 1 ? args[1] : Path.Combine(corpusRoot, "knowledge");
var perSongTimeoutMinutes = args.Length > 2 ? double.Parse(args[2]) : CorpusMiningRunner.DefaultPerSongTimeout.TotalMinutes;
var perSongTimeout = TimeSpan.FromMinutes(perSongTimeoutMinutes);
var maxSongConcurrency = args.Length > 3 ? int.Parse(args[3]) : CorpusMiningRunner.DefaultMaxSongConcurrency;
var seasonCommits = args.Length > 4 && !string.IsNullOrWhiteSpace(args[4])
    ? args[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : [];

var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");

// Build-plan Phase 17 §2a: exclude this app's own prior project directories, same as the Shell UI's
// own "Run corpus mining" action -- so this standalone CLI run gets the same exclusion the real
// 2026-10-31 combined re-run documented in the Phase 17 build plan is expected to use going forward.
var excludedProjectRootDirectories = new RecentProjectsStore(appDataDirectory).Load().Select(p => p.RootDirectory).ToList();
var roleClientResolver = new AiRoleClientResolver(
    new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers")),
    CredentialStoreFactory.Create(Path.Combine(appDataDirectory, "credentials")),
    new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

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
Log($"Max song concurrency: {(maxSongConcurrency <= 0 ? $"every available processor ({Environment.ProcessorCount})" : maxSongConcurrency.ToString())}");
Log($"Log file: {logPath}");

var stopwatch = Stopwatch.StartNew();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Log("Cancellation requested (Ctrl+C) -- stopping after the current song.");
    cts.Cancel();
};

IExemplarDescriptionClient? descriptionClient = null;
try
{
    var (client, model) = await roleClientResolver.ResolveRequiredAsync(AiRole.Director, cts.Token);
    Log($"Director role configured (model={model}) -- real exemplar descriptions will be generated for this run.");
    descriptionClient = new ChatCompletionExemplarDescriptionClient(client, model, new JsonAiCallLogStore(Path.Combine(knowledgeDirectory, "llm")));
}
catch (AiProviderNotConfiguredException)
{
    Log("Director role not configured under this user's AI provider settings -- exemplar descriptions will be skipped for this run.");
}

CorpusMiningResult result;
try
{
    result = seasonCommits.Length > 0
        ? await SeasonalCorpusMiningOrchestrator.RunAsync(
            corpusRoot, corpusRoot, seasonCommits, Path.Combine(knowledgeDirectory, "season-scratch"),
            knowledgeDirectory, perSongTimeout, Log, cts.Token, descriptionClient, maxSongConcurrency,
            excludedProjectRootDirectories)
        : await CorpusMiningRunner.RunAsync(
            corpusRoot, knowledgeDirectory, perSongTimeout, Log, cts.Token, descriptionClient, maxSongConcurrency,
            excludedProjectRootDirectories: excludedProjectRootDirectories);
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
Log($"  With real BandBalance: {result.ExemplarIndex.Entries.Count(e => e.Fingerprint.BandBalance is not null)}/{result.ExemplarIndex.Entries.Count}");
Log($"  With real VocalActivityHint: {result.ExemplarIndex.Entries.Count(e => e.Fingerprint.VocalActivityHint is not null)}/{result.ExemplarIndex.Entries.Count}");
Log($"  With real Description: {result.ExemplarIndex.Entries.Count(e => !string.IsNullOrWhiteSpace(e.Description))}/{result.ExemplarIndex.Entries.Count}");
foreach (var kind in Enum.GetValues<XlightsAI.Studio.Structure.SectionKind>())
{
    var available = result.ExemplarIndex.Entries.Count(e => e.Fingerprint.SectionKind == kind);
    Log($"    {kind,-10} entries={available,4}  DirectorPromptBuilder would request {XlightsAI.Studio.Director.DirectorPromptBuilder.ExemplarCountForKind(result.ExemplarIndex, kind)}");
}

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
