using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Xlights;

// Build-plan Phase 18 §2 (Phase A) one-off runner: "for every real effect name in the xLights
// vocabulary, produce a tiny, isolated synthetic sequence that exercises it on its own... persisted
// somewhere reviewable (e.g. a manifest file) before any rendering is attempted." Deliberately stops
// there -- Phase B (real headless render per fixture) and beyond are separate, not-yet-implemented
// phases per the build plan's own "one phase at a time, confirm before the next" discipline; this tool
// never launches real xLights.
//
// Usage: dotnet run -c Release --project tools/EffectCharacterizer -- [effectMetadataDirectory] [outputDirectory] [durationSeconds]
// Defaults: effectMetadataDirectory=C:\Program Files\xLights\effectmetadata,
// outputDirectory=%AppData%\XlightsAI.Studio\effect-characterization, durationSeconds=6

var effectMetadataDirectory = args.Length > 0 ? args[0] : @"C:\Program Files\xLights\effectmetadata";
var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");
var outputDirectory = args.Length > 1 ? args[1] : Path.Combine(appDataDirectory, "effect-characterization");
var durationSeconds = args.Length > 2 ? double.Parse(args[2]) : EffectFixturePlanner.DefaultFixtureDurationSeconds;

Directory.CreateDirectory(outputDirectory);
var logPath = Path.Combine(outputDirectory, $"effect-characterizer-{DateTime.Now:yyyyMMdd-HHmmss}.log");
await using var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

void Log(string message)
{
    var line = $"[{DateTimeOffset.Now:O}] {message}";
    Console.WriteLine(line);
    logWriter.WriteLine(line);
}

Log($"Effect metadata directory: {effectMetadataDirectory}");
Log($"Output directory: {outputDirectory}");
Log($"Fixture duration: {durationSeconds:F1}s");
Log($"Log file: {logPath}");

if (!Directory.Exists(effectMetadataDirectory))
{
    Log($"FATAL: effect metadata directory does not exist: {effectMetadataDirectory}");
    return 1;
}

try
{
    var (vocabulary, recipes) = EffectCatalogFactory.BuildFromMetadataDirectory(effectMetadataDirectory);
    Log($"Loaded real effect vocabulary: {vocabulary.Effects.Count} effect(s).");

    var manifest = EffectFixturePlanner.Build(vocabulary, recipes, effectMetadataDirectory, durationSeconds);
    Log($"Built {manifest.Fixtures.Count} fixture plan(s) ({manifest.SucceededCount} compiled, {manifest.FailedCount} failed).");

    if (manifest.FailedCount > 0)
    {
        Log("Failures:");
        foreach (var failure in manifest.Fixtures.Where(f => !f.CompiledSuccessfully))
        {
            Log($"  - {failure.EffectName} ({failure.ResolutionTier}): {failure.CompileFailureReason}");
        }
    }

    var store = new JsonEffectFixtureManifestStore(outputDirectory);
    var manifestPath = await store.SaveAsync(manifest, CancellationToken.None);

    Log("Artifacts written:");
    Log($"  - {manifestPath}");
    Log($"  - {logPath}");

    return 0;
}
catch (Exception ex)
{
    Log($"FATAL: {ex}");
    return 1;
}
