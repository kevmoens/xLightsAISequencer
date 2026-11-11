using System.Text.Json;
using System.Text.Json.Serialization;
using XlightsAI.Studio.AiEdits;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Director;
using XlightsAI.Studio.Knowledge;

// Build-plan Phase 18 §6 (Phase E) task 3: "A new standalone offline tool... that runs Phases A-D end to end
// and writes this catalog -- never something that runs automatically during ordinary generation."
//
// Runs the exact same A-D pipeline as tools/EffectCharacterizer (shared via
// XlightsAI.Studio.Director.EffectCharacterizationPipelineRunner -- never a duplicated implementation), then
// converts the resulting EffectVisualCharacterizationManifest into the shippable, committed
// EffectVisualCharacterCatalog (EffectVisualCharacterCatalogBuilder -- the DMX/Servo/Moving Head exclusion
// and the hardcoded, already-verified manual.xlights.org documented-behavior quotes live there) and writes
// it to XlightsAI.Studio.Knowledge's own SeedData file. That file is committed to the repo like any other
// hand-curated knowledge artifact (review the diff, then commit) -- it is loaded at runtime from the
// EmbeddedResource baked into the Knowledge assembly (EffectVisualCharacterCatalogLoader), never from
// %AppData%, since this catalog is core, rarely-changing knowledge, not a per-machine runtime artifact.
//
// Usage: dotnet run -c Release --project tools/EffectCatalogBuilder -- [effectMetadataDirectory] [pipelineOutputDirectory] [seedFilePath] [durationSeconds] [xlightsExecutablePath] [--skip-render] [--skip-ai] [--effects=Name1,Name2,...] [--convert-only]
// Defaults: effectMetadataDirectory=C:\Program Files\xLights\effectmetadata,
// pipelineOutputDirectory=%AppData%\XlightsAI.Studio\effect-characterization (same directory
// tools/EffectCharacterizer already writes to -- --convert-only reads an already-completed sweep from here),
// seedFilePath=src\XlightsAI.Studio.Knowledge\SeedData\effect-visual-characters.json (relative to the
// current working directory -- run this tool from the repo root, same convention as every other tool here),
// durationSeconds=6, xlightsExecutablePath=C:\Program Files\xLights\xlights.exe.
//
// --convert-only skips Phases A-D entirely and just loads the EffectVisualCharacterizationManifest already
// persisted at pipelineOutputDirectory (e.g. from a prior tools/EffectCharacterizer or tools/EffectCatalogBuilder
// run) -- useful for regenerating the committed catalog after only editing
// EffectVisualCharacterCatalogBuilder's own documented-behavior table, without re-running an hours-long real
// xLights sweep.

var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var skipRender = args.Contains("--skip-render", StringComparer.Ordinal);
var skipAi = args.Contains("--skip-ai", StringComparer.Ordinal);
var convertOnly = args.Contains("--convert-only", StringComparer.Ordinal);
var effectsFilterArg = args.FirstOrDefault(a => a.StartsWith("--effects=", StringComparison.Ordinal));
var effectsFilter = effectsFilterArg?["--effects=".Length..]
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet(StringComparer.Ordinal);

var effectMetadataDirectory = positional.Length > 0 ? positional[0] : @"C:\Program Files\xLights\effectmetadata";
var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XlightsAI.Studio");
var pipelineOutputDirectory = positional.Length > 1 ? positional[1] : Path.Combine(appDataDirectory, "effect-characterization");
var seedFilePath = positional.Length > 2 ? positional[2] : Path.Combine("src", "XlightsAI.Studio.Knowledge", "SeedData", "effect-visual-characters.json");
var durationSeconds = positional.Length > 3 ? double.Parse(positional[3]) : EffectFixturePlanner.DefaultFixtureDurationSeconds;
var xlightsExecutablePath = positional.Length > 4 ? positional[4] : @"C:\Program Files\xLights\xlights.exe";

void Log(string message) => Console.WriteLine($"[{DateTimeOffset.Now:O}] {message}");

Log($"Effect metadata directory: {effectMetadataDirectory}");
Log($"Pipeline output directory: {pipelineOutputDirectory}");
Log($"Seed file: {seedFilePath}");

try
{
    EffectVisualCharacterizationManifest characterizationManifest;

    if (convertOnly)
    {
        Log("--convert-only: skipping Phases A-D, loading an already-persisted characterization manifest.");
        characterizationManifest = await new JsonEffectVisualCharacterizationManifestStore(pipelineOutputDirectory)
            .LoadAsync(CancellationToken.None);
    }
    else
    {
        if (!Directory.Exists(effectMetadataDirectory))
        {
            Log($"FATAL: effect metadata directory does not exist: {effectMetadataDirectory}");
            return 1;
        }

        IVisionCapableChatCompletionClient? visionClient = null;
        string? visionModelName = null;
        if (!skipAi)
        {
            var roleProviderConfigStore = new JsonRoleProviderConfigStore(Path.Combine(appDataDirectory, "ai-providers"));
            var credentialStore = CredentialStoreFactory.Create(Path.Combine(appDataDirectory, "credentials"));
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
                    Log($"Phase D: SKIPPED -- the Critic role's configured provider (model '{criticModelName}') does not support image input.");
                }
            }
            catch (AiProviderNotConfiguredException ex)
            {
                Log($"Phase D: SKIPPED -- {ex.Message}");
            }
        }

        var result = await EffectCharacterizationPipelineRunner.RunAsync(
            effectMetadataDirectory, pipelineOutputDirectory, durationSeconds, xlightsExecutablePath,
            skipRender, skipAi, effectsFilter, visionClient, visionModelName, Log, CancellationToken.None);

        if (result.CharacterizationManifest is null)
        {
            Log("FATAL: Phase D never produced a characterization manifest (see the SKIPPED/UNAVAILABLE reason above) -- nothing to build a catalog from.");
            return 1;
        }

        characterizationManifest = result.CharacterizationManifest;
    }

    var catalog = EffectVisualCharacterCatalogBuilder.Build(characterizationManifest);
    Log($"Built catalog: {catalog.Characters.Count} entries from {characterizationManifest.Outcomes.Count} characterization outcome(s) " +
        $"({EffectVisualCharacterCatalogBuilder.HardwareOnlyEffectNames.Count} hardware-only effect name(s) excluded).");

    var seedDirectory = Path.GetDirectoryName(Path.GetFullPath(seedFilePath));
    if (seedDirectory is not null)
    {
        Directory.CreateDirectory(seedDirectory);
    }

    var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    });
    await File.WriteAllTextAsync(seedFilePath, json, CancellationToken.None);
    Log($"Wrote catalog: {Path.GetFullPath(seedFilePath)}");
    Log("Review the diff and commit it like any other hand-curated knowledge artifact.");

    return 0;
}
catch (Exception ex)
{
    Log($"FATAL: {ex}");
    return 1;
}
