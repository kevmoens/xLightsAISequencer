using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Director;

// Build-plan Phase 18 §2-§5 (Phase A-D) end-to-end runner.
//
// Phase A: "for every real effect name in the xLights vocabulary, produce a tiny, isolated synthetic
// sequence that exercises it on its own... persisted somewhere reviewable (e.g. a manifest file) before any
// rendering is attempted."
//
// Phase B: "actually run each fixture through real xLights, the same way the Quality Pass feature already
// does for a whole song, just scoped down to one effect at a time" -- exports every successfully-compiled
// fixture to a real .xsq, renders it via the real headless render path, and decodes/persists its real
// PreviewFrames. Requires a real, configured xLights executable; never falls back to a synthetic/approximate
// renderer (EffectFixtureRenderUnavailableException surfaces this the same way QualityPassUnavailableException
// already does elsewhere in this codebase).
//
// Phase C: "most effects are defined by their motion, which isolated still frames lose" -- rasterizes every
// real decoded PreviewFrame from a Rendered fixture and encodes the full sequence into a real, playable
// animated GIF (EffectFixtureVideoEncoder), alongside that fixture's own fixture.xsq/frames.json.
//
// Phase D: "turn each real video/frame sequence into a concise, structured, reusable text description of
// what the effect actually does" -- resolves the real app's own configured Critic-role vision-capable client
// (the same AiRoleClientResolver/AiRole.Critic path AiChoreographyWholeSongGenerator's own Quality Pass
// wiring already uses, never a second client-construction path), samples a small set of evenly-spaced real
// stills per fixture (every real client here only accepts PNG images, not video/GIF -- confirmed, not
// assumed), and persists each fixture's own real AI-generated description via EffectVisualCharacterizer.
//
// Build-plan Phase 18 §6 (Phase E) task 3: Phases A-D's own sequencing/logging/persistence now live in
// XlightsAI.Studio.Director's EffectCharacterizationPipelineRunner -- this Program.cs only owns CLI parsing
// and this tool's own AI-role resolution, so tools/EffectCatalogBuilder can run the exact same pipeline
// without duplicating it. Phase E/F (persisting the shippable catalog into real generation, per-layer
// annotation) are handled by that separate tool / a later phase.
//
// Disclosed cost (build plan §3): a full vocabulary sweep launches real xLights once per effect/tier
// combination -- 100+ real renders, likely minutes-to-hours depending on machine.
//
// Usage: dotnet run -c Release --project tools/EffectCharacterizer -- [effectMetadataDirectory] [outputDirectory] [durationSeconds] [xlightsExecutablePath] [--skip-render] [--skip-ai] [--effects=Name1,Name2,...]
// Defaults: effectMetadataDirectory=C:\Program Files\xLights\effectmetadata,
// outputDirectory=%AppData%\XlightsAI.Studio\effect-characterization, durationSeconds=6,
// xlightsExecutablePath=C:\Program Files\xLights\xlights.exe (used only if it exists)
//
// --effects restricts Phase D (the only phase that spends real, billed AI calls) to a named subset of
// effects -- Phases A/B/C still run against the full vocabulary (free, fast, and worth keeping complete for
// a later full Phase D sweep). Lets a real cost/quality spot-check happen before committing to the whole
// catalog, per the build plan's own "manual spot-check... before the catalog is trusted" discipline.

var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var skipRender = args.Contains("--skip-render", StringComparer.Ordinal);
var skipAi = args.Contains("--skip-ai", StringComparer.Ordinal);
var recomputeBrightness = args.Contains("--recompute-brightness", StringComparer.Ordinal);
var effectsFilterArg = args.FirstOrDefault(a => a.StartsWith("--effects=", StringComparison.Ordinal));
var effectsFilter = effectsFilterArg?["--effects=".Length..]
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);

var effectMetadataDirectory = positional.Length > 0 ? positional[0] : @"C:\Program Files\xLights\effectmetadata";
var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");
var outputDirectory = positional.Length > 1 ? positional[1] : Path.Combine(appDataDirectory, "effect-characterization");
var durationSeconds = positional.Length > 2 ? double.Parse(positional[2]) : EffectFixturePlanner.DefaultFixtureDurationSeconds;
var xlightsExecutablePath = positional.Length > 3 ? positional[3] : @"C:\Program Files\xLights\xlights.exe";

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
Log($"xLights executable: {xlightsExecutablePath}{(skipRender ? " (Phase B skipped by --skip-render)" : "")}");
Log($"Log file: {logPath}");

if (recomputeBrightness)
{
    // Build-plan Phase 21 §2a/§2c: augments an already-completed sweep's persisted characterization manifest
    // with real MeanBrightness/PeakBrightness, reading the SAME frames.json Phase B already decoded -- never
    // a re-render, never a second AI call. Useful for adding this phase's new measurement to a catalog whose
    // Description text was already generated (and billed) by a prior tools/EffectCharacterizer run.
    try
    {
        var renderManifest = await new JsonEffectFixtureRenderManifestStore(outputDirectory).LoadAsync(CancellationToken.None);
        var existingManifest = await new JsonEffectVisualCharacterizationManifestStore(outputDirectory).LoadAsync(CancellationToken.None);

        var updatedManifest = await EffectVisualCharacterizer.RecomputeBrightnessAsync(existingManifest, renderManifest, Log, CancellationToken.None);

        var path = await new JsonEffectVisualCharacterizationManifestStore(outputDirectory).SaveAsync(updatedManifest, CancellationToken.None);
        Log($"Recomputed brightness for {updatedManifest.Outcomes.Count(o => o.MeanBrightness is not null)} of {updatedManifest.Outcomes.Count} outcome(s).");
        Log($"Updated characterization manifest: {path}");
        return 0;
    }
    catch (Exception ex)
    {
        Log($"FATAL: {ex}");
        return 1;
    }
}

if (!Directory.Exists(effectMetadataDirectory))
{
    Log($"FATAL: effect metadata directory does not exist: {effectMetadataDirectory}");
    return 1;
}

try
{
    var overallStopwatch = System.Diagnostics.Stopwatch.StartNew();

    IVisionCapableChatCompletionClient? visionClient = null;
    string? visionModelName = null;
    if (!skipAi)
    {
        var roleProviderConfigStore = new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers"));
        var credentialStore = CredentialStoreFactory.Create(Path.Combine(appDataDirectory, "credentials"));
        // Same HttpClient.Timeout workaround GenerateOnce already applies: the production composition root wires
        // AiRoleClientResolver with a plain `new HttpClient()` (100s default timeout) that fires independently of
        // (and tighter than) RoleProviderConfig.RequestTimeoutSeconds -- real multimodal calls with several
        // images easily exceed 100s against a real local model under load.
        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var aiRoleClientResolver = new AiRoleClientResolver(roleProviderConfigStore, credentialStore, httpClient);

        try
        {
            var (criticClient, criticModelName) = await aiRoleClientResolver.ResolveRequiredAsync(AiRole.Critic, CancellationToken.None);
            if (criticClient is IVisionCapableChatCompletionClient resolvedVisionClient)
            {
                visionClient = resolvedVisionClient;
                visionModelName = criticModelName;
            }
            else
            {
                Log($"Phase D: SKIPPED -- the Critic role's configured provider (model '{criticModelName}') does not " +
                    "support image input. Configure a vision-capable provider (e.g. Anthropic, or an OpenAI-compatible " +
                    "vision model) for the Critic role, or pass --skip-ai.");
            }
        }
        catch (AiProviderNotConfiguredException ex)
        {
            Log($"Phase D: SKIPPED -- {ex.Message} Pass --skip-ai to silence this, or configure the Critic role " +
                "in Settings. Phase D never substitutes a fake/local description generator for a real AI call.");
        }
    }

    var result = await EffectCharacterizationPipelineRunner.RunAsync(
        effectMetadataDirectory, outputDirectory, durationSeconds, xlightsExecutablePath,
        skipRender, skipAi, effectsFilter, visionClient, visionModelName, Log, CancellationToken.None);

    overallStopwatch.Stop();
    Log($"Total wall-clock time: {overallStopwatch.Elapsed.TotalSeconds:F1}s.");
    Log("Artifacts written:");
    Log($"  - {result.FixtureManifestPath}");
    if (result.RenderManifestPath is not null) Log($"  - {result.RenderManifestPath}");
    if (result.VideoManifestPath is not null) Log($"  - {result.VideoManifestPath}");
    if (result.CharacterizationManifestPath is not null) Log($"  - {result.CharacterizationManifestPath}");
    Log($"  - {logPath}");

    return 0;
}
catch (Exception ex)
{
    Log($"FATAL: {ex}");
    return 1;
}
