using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 18 §6 (Phase E) task 3: the real A-&gt;B-&gt;C-&gt;D pipeline (fixture planning, real
/// headless render, GIF encode, AI characterization), extracted out of <c>tools/EffectCharacterizer</c>'s
/// own <c>Program.cs</c> into a reusable library method so both that tool and <c>tools/EffectCatalogBuilder</c>
/// can run the exact same orchestration without duplicating it. Every phase's own real logic/type
/// (<see cref="EffectFixturePlanner"/>/<see cref="EffectFixtureRenderer"/>/<see cref="EffectFixtureVideoEncoder"/>/
/// <see cref="EffectVisualCharacterizer"/>) is unchanged -- this only extracts the sequencing, logging, and
/// persistence glue that previously lived directly in the tool's <c>Program.cs</c>.
/// </summary>
public static class EffectCharacterizationPipelineRunner
{
    /// <summary>Every artifact this run produced -- a manifest/path pair is null exactly when that phase never ran (skipped by caller request, or a later phase was never reached because an earlier one was unavailable).</summary>
    public sealed record Result(
        EffectFixtureManifest FixtureManifest,
        string FixtureManifestPath,
        EffectFixtureRenderManifest? RenderManifest,
        string? RenderManifestPath,
        EffectFixtureVideoManifest? VideoManifest,
        string? VideoManifestPath,
        EffectVisualCharacterizationManifest? CharacterizationManifest,
        string? CharacterizationManifestPath);

    /// <param name="effectMetadataDirectory">Real xLights effectmetadata directory -- the same directory <see cref="XlightsAI.Studio.Choreography.EffectCatalogFactory.BuildFromMetadataDirectory"/> already requires.</param>
    /// <param name="outputDirectory">Where every phase's own manifest/fixture/render/video artifact is written -- same single directory convention <c>tools/EffectCharacterizer</c> already uses.</param>
    /// <param name="durationSeconds">Per-fixture synthetic sequence duration -- see <see cref="EffectFixturePlanner.DefaultFixtureDurationSeconds"/>.</param>
    /// <param name="xlightsExecutablePath">A real xLights executable for Phase B's headless render. When it does not exist on disk, Phase B (and everything after it) is skipped -- never a synthetic/approximate renderer substitute.</param>
    /// <param name="skipRender">Stops after Phase A -- the same <c>--skip-render</c> behavior <c>tools/EffectCharacterizer</c> already exposes.</param>
    /// <param name="skipAi">Stops after Phase C -- the same <c>--skip-ai</c> behavior.</param>
    /// <param name="effectsFilter">Restricts Phase D (the only phase that spends real, billed AI calls) to a named subset of effects; Phases A/B/C still cover the full vocabulary. Null/empty means no restriction.</param>
    /// <param name="visionClient">The real, already-resolved vision-capable client for Phase D, or null to skip Phase D (same as <paramref name="skipAi"/>) -- callers resolve this however they resolve AI roles (this method does no role/provider resolution of its own).</param>
    /// <param name="visionModelName">The vision client's configured model name, required when <paramref name="visionClient"/> is non-null.</param>
    /// <param name="log">Optional progress sink -- every phase's own existing log lines are preserved verbatim.</param>
    public static async Task<Result> RunAsync(
        string effectMetadataDirectory,
        string outputDirectory,
        double durationSeconds,
        string xlightsExecutablePath,
        bool skipRender,
        bool skipAi,
        IReadOnlySet<string>? effectsFilter,
        IVisionCapableChatCompletionClient? visionClient,
        string? visionModelName,
        Action<string>? log,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectMetadataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(xlightsExecutablePath);

        Directory.CreateDirectory(outputDirectory);

        var (vocabulary, recipes) = EffectCatalogFactory.BuildFromMetadataDirectory(effectMetadataDirectory);
        log?.Invoke($"Loaded real effect vocabulary: {vocabulary.Effects.Count} effect(s).");

        // Phase A.
        var manifest = EffectFixturePlanner.Build(vocabulary, recipes, effectMetadataDirectory, durationSeconds);
        log?.Invoke($"Phase A: built {manifest.Fixtures.Count} fixture plan(s) ({manifest.SucceededCount} compiled, {manifest.FailedCount} failed).");

        if (manifest.FailedCount > 0)
        {
            log?.Invoke("Phase A failures:");
            foreach (var failure in manifest.Fixtures.Where(f => !f.CompiledSuccessfully))
            {
                log?.Invoke($"  - {failure.EffectName} ({failure.ResolutionTier}): {failure.CompileFailureReason}");
            }
        }

        var manifestPath = await new JsonEffectFixtureManifestStore(outputDirectory).SaveAsync(manifest, ct).ConfigureAwait(false);
        log?.Invoke($"Phase A artifact: {manifestPath}");

        if (skipRender)
        {
            log?.Invoke("Phase B: skipped (--skip-render was passed).");
            return new Result(manifest, manifestPath, null, null, null, null, null, null);
        }

        if (!File.Exists(xlightsExecutablePath))
        {
            log?.Invoke($"Phase B: SKIPPED -- no real xLights executable found at '{xlightsExecutablePath}'. " +
                "Pass a real, configured xLights executable path, or skip rendering. Phase B never substitutes a " +
                "synthetic/approximate renderer for a real one.");
            return new Result(manifest, manifestPath, null, null, null, null, null, null);
        }

        log?.Invoke("Phase B: starting real headless render per fixture. This launches real xLights once per " +
            $"successfully-compiled fixture ({manifest.SucceededCount} expected) -- expect minutes-to-hours depending on machine.");

        EffectFixtureRenderManifest renderManifest;
        try
        {
            renderManifest = await EffectFixtureRenderer.RenderAsync(
                manifest, vocabulary, recipes, xlightsExecutablePath, outputDirectory, log, ct).ConfigureAwait(false);
        }
        catch (EffectFixtureRenderUnavailableException ex)
        {
            log?.Invoke($"Phase B: UNAVAILABLE -- {ex.Message}");
            return new Result(manifest, manifestPath, null, null, null, null, null, null);
        }

        log?.Invoke($"Phase B: {renderManifest.RenderedCount} rendered, {renderManifest.FailedCount} failed, out of {renderManifest.Outcomes.Count} fixture(s).");
        if (renderManifest.FailedCount > 0)
        {
            log?.Invoke("Phase B failures:");
            foreach (var failure in renderManifest.Outcomes.Where(o => o.Status != EffectFixtureRenderStatus.Rendered))
            {
                log?.Invoke($"  - {failure.EffectName} ({failure.ResolutionTier}) [{failure.Status}]: {failure.FailureReason}");
            }
        }

        var renderManifestPath = await new JsonEffectFixtureRenderManifestStore(outputDirectory).SaveAsync(renderManifest, ct).ConfigureAwait(false);
        log?.Invoke($"Phase B artifact: {renderManifestPath}");

        // Phase C.
        log?.Invoke($"Phase C: starting GIF encoding for every Rendered fixture ({renderManifest.RenderedCount} expected).");
        var videoManifest = await EffectFixtureVideoEncoder.EncodeAsync(renderManifest, log, ct).ConfigureAwait(false);
        log?.Invoke($"Phase C: {videoManifest.EncodedCount} encoded, {videoManifest.FailedCount} failed, out of {videoManifest.Outcomes.Count} fixture(s).");

        if (videoManifest.FailedCount > 0)
        {
            log?.Invoke("Phase C failures:");
            foreach (var failure in videoManifest.Outcomes.Where(o => o.Status != EffectFixtureVideoStatus.Encoded))
            {
                log?.Invoke($"  - {failure.EffectName} ({failure.ResolutionTier}) [{failure.Status}]: {failure.FailureReason}");
            }
        }

        var videoManifestPath = await new JsonEffectFixtureVideoManifestStore(outputDirectory).SaveAsync(videoManifest, ct).ConfigureAwait(false);
        log?.Invoke($"Phase C artifact: {videoManifestPath}");

        // Phase D.
        if (skipAi || visionClient is null)
        {
            log?.Invoke(skipAi ? "Phase D: skipped (--skip-ai was passed)." : "Phase D: skipped (no vision-capable client supplied).");
            return new Result(manifest, manifestPath, renderManifest, renderManifestPath, videoManifest, videoManifestPath, null, null);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(visionModelName);

        var characterizationRenderManifest = effectsFilter is { Count: > 0 }
            ? renderManifest with { Outcomes = renderManifest.Outcomes.Where(o => effectsFilter.Contains(o.EffectName)).ToList() }
            : renderManifest;

        if (effectsFilter is { Count: > 0 })
        {
            log?.Invoke($"Phase D: restricted to {effectsFilter.Count} named effect(s) ({characterizationRenderManifest.Outcomes.Count} fixture(s) in scope) -- Phases A/B/C above still covered the full vocabulary.");
        }

        log?.Invoke($"Phase D: starting AI visual characterization for every Rendered fixture in scope ({characterizationRenderManifest.RenderedCount} expected), using model '{visionModelName}'.");
        var characterizationManifest = await EffectVisualCharacterizer.CharacterizeAsync(
            characterizationRenderManifest, visionClient, visionModelName, log, ct).ConfigureAwait(false);
        log?.Invoke($"Phase D: {characterizationManifest.CharacterizedCount} characterized, {characterizationManifest.FailedCount} failed, out of {characterizationManifest.Outcomes.Count} fixture(s).");

        if (characterizationManifest.FailedCount > 0)
        {
            log?.Invoke("Phase D failures:");
            foreach (var failure in characterizationManifest.Outcomes.Where(o => o.Status != EffectVisualCharacterizationStatus.Characterized))
            {
                log?.Invoke($"  - {failure.EffectName} ({failure.ResolutionTier}) [{failure.Status}]: {failure.FailureReason}");
            }
        }

        var characterizationManifestPath = await new JsonEffectVisualCharacterizationManifestStore(outputDirectory).SaveAsync(characterizationManifest, ct).ConfigureAwait(false);
        log?.Invoke($"Phase D artifact: {characterizationManifestPath}");

        return new Result(
            manifest, manifestPath, renderManifest, renderManifestPath, videoManifest, videoManifestPath,
            characterizationManifest, characterizationManifestPath);
    }
}
