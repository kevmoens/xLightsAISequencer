using System.Diagnostics;
using System.Text.Json;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Rendering;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §3 (Phase B): "actually run each fixture through real xLights, the same way the
/// Quality Pass feature already does for a whole song, just scoped down to one effect at a time." Reuses the
/// exact real export (<see cref="XsqExporter"/>) and real headless render path
/// (<see cref="RenderPipeline"/>/<see cref="XlightsRenderAdapter"/>/<see cref="XlightsRenderCapabilityProbe"/>)
/// the Quality Pass feature (<c>ChoreographyGenerationPipeline</c>'s own <c>EnableQualityPass</c> branch)
/// already uses -- never a second render implementation. Every fixture's own outcome is recorded, success or
/// an explicit failure reason, per this phase's own "never a silently skipped entry" acceptance criterion.
/// </summary>
public static class EffectFixtureRenderer
{
    private const string TargetXlightsVersion = "2026.03";
    private static readonly JsonSerializerOptions FramesJsonOptions = new() { WriteIndented = false };

    /// <param name="manifest">Phase A's own compiled manifest -- every <see cref="EffectFixturePlan"/> it lists is accounted for in the returned <see cref="EffectFixtureRenderManifest"/>, never silently dropped.</param>
    /// <param name="vocabulary">The same real effect vocabulary Phase A compiled <paramref name="manifest"/> against.</param>
    /// <param name="recipes">The same real recipe catalog Phase A compiled <paramref name="manifest"/> against.</param>
    /// <param name="xlightsExecutablePath">A real, configured xLights executable. Required -- see <see cref="EffectFixtureRenderUnavailableException"/>.</param>
    /// <param name="outputRootDirectory">Where this run's own synthetic show, per-fixture exports/renders, and decoded frame files are written.</param>
    /// <param name="log">Optional progress sink (console + log-file convention, same as <c>tools/EffectCharacterizer</c> already uses).</param>
    /// <exception cref="EffectFixtureRenderUnavailableException">No real, headless-capable xLights executable is configured -- this phase cannot run without one, and never falls back to a synthetic/approximate renderer.</exception>
    public static async Task<EffectFixtureRenderManifest> RenderAsync(
        EffectFixtureManifest manifest,
        EffectVocabularyCatalog vocabulary,
        EffectRecipeCatalog recipes,
        string xlightsExecutablePath,
        string outputRootDirectory,
        Action<string>? log,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentException.ThrowIfNullOrWhiteSpace(xlightsExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootDirectory);

        var probe = new XlightsRenderCapabilityProbe();
        RenderCapability capability;
        try
        {
            capability = await probe.ProbeAsync(xlightsExecutablePath, ct).ConfigureAwait(false);
        }
        catch (RenderCapabilityProbeInvalidException ex)
        {
            throw new EffectFixtureRenderUnavailableException(
                $"Phase B requires a real, configured xLights executable to render against, but probing " +
                $"'{xlightsExecutablePath}' failed: {ex.Message}");
        }

        if (!capability.SupportsHeadless)
        {
            throw new EffectFixtureRenderUnavailableException(
                $"The configured xLights executable at '{xlightsExecutablePath}' (version {capability.XlightsVersion}) " +
                "did not report supporting a real headless render during capability probing. Phase B cannot run " +
                "without one -- install/configure a build that supports '--headless', never a synthetic/approximate renderer.");
        }

        var showDirectory = Path.Combine(outputRootDirectory, "synthetic-show");
        EffectFixtureSyntheticShow.Write(showDirectory);
        var audioPath = EffectFixtureSyntheticShow.WriteAudioTrack(showDirectory);

        var layout = EffectFixturePlanner.BuildFixtureLayout();
        var exporter = new XsqExporter(recipes, vocabulary);
        var renderPipeline = new RenderPipeline(
            new XlightsRenderAdapter(probe, xlightsExecutablePath), new FseqRenderedFrameDecoder(), new XsqStructuralReader());

        var outcomes = new List<EffectFixtureRenderOutcome>(manifest.Fixtures.Count);
        var fixtureIndex = 0;

        foreach (var plan in manifest.Fixtures)
        {
            fixtureIndex++;
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await RenderOneAsync(
                plan, fixtureIndex, manifest.Fixtures.Count, vocabulary, recipes, layout, exporter, renderPipeline,
                showDirectory, audioPath, outputRootDirectory, log, ct).ConfigureAwait(false));
        }

        return new EffectFixtureRenderManifest(DateTimeOffset.UtcNow, xlightsExecutablePath, showDirectory, outcomes);
    }

    private static async Task<EffectFixtureRenderOutcome> RenderOneAsync(
        EffectFixturePlan plan, int fixtureIndex, int fixtureCount, EffectVocabularyCatalog vocabulary,
        EffectRecipeCatalog recipes, DisplayGraph layout, XsqExporter exporter, RenderPipeline renderPipeline,
        string showDirectory, string audioPath, string outputRootDirectory, Action<string>? log, CancellationToken ct)
    {
        var label = $"[{fixtureIndex}/{fixtureCount}] {plan.EffectName} ({plan.ResolutionTier})";

        if (!plan.CompiledSuccessfully)
        {
            var reason = $"Phase A compile failed: {plan.CompileFailureReason}";
            log?.Invoke($"{label}: SKIPPED (compile already failed) -- {reason}");
            return new EffectFixtureRenderOutcome(
                plan.EffectName, plan.ResolutionTier, EffectFixtureRenderStatus.CompileSkipped, reason,
                ExportedXsqPath: null, FseqPath: null, FramesPath: null, FrameCount: 0, WallClockSeconds: 0);
        }

        var fixtureDirectoryName = $"{Sanitize(plan.EffectName)}-{plan.ResolutionTier}".ToLowerInvariant();
        var fixtureDirectory = Path.Combine(outputRootDirectory, "fixtures", fixtureDirectoryName);
        Directory.CreateDirectory(fixtureDirectory);

        var stopwatch = Stopwatch.StartNew();

        string exportedXsqPath;
        try
        {
            var ir = EffectFixturePlanner.CompileLook(plan, vocabulary, recipes);
            var metadata = new SequenceMetadata(Song: $"{plan.EffectName}-{plan.ResolutionTier}", MediaFile: audioPath);
            var exportPath = Path.Combine(fixtureDirectory, "fixture.xsq");
            var exportResult = await exporter.ExportAsync(
                layout, ir.ToSequenceDocument(metadata), exportPath, TargetXlightsVersion,
                XsqExportPolicy.OverwriteWithBackup, ct).ConfigureAwait(false);
            exportedXsqPath = exportResult.Path;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"{label}: EXPORT FAILED -- {ex.Message}");
            return new EffectFixtureRenderOutcome(
                plan.EffectName, plan.ResolutionTier, EffectFixtureRenderStatus.ExportFailed, ex.Message,
                ExportedXsqPath: null, FseqPath: null, FramesPath: null, FrameCount: 0, WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }

        RenderPipelineResult renderResult;
        try
        {
            var renderOutputDirectory = Path.Combine(fixtureDirectory, "render");
            Directory.CreateDirectory(renderOutputDirectory);
            var request = new RenderRequest(
                Guid.NewGuid(), showDirectory, exportedXsqPath, renderOutputDirectory, Selection: null, RenderAuthority.ActualXlights);
            renderResult = await renderPipeline.RunAsync(request, layout, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"{label}: RENDER FAILED -- {ex.Message}");
            return new EffectFixtureRenderOutcome(
                plan.EffectName, plan.ResolutionTier, EffectFixtureRenderStatus.RenderFailed, ex.Message,
                exportedXsqPath, FseqPath: null, FramesPath: null, FrameCount: 0, WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }

        if (!renderResult.Artifact.Success || renderResult.Frames is null)
        {
            var reason = renderResult.Artifact.Warnings.Count > 0
                ? string.Join(" | ", renderResult.Artifact.Warnings)
                : "Render reported failure with no further warnings.";
            log?.Invoke($"{label}: RENDER FAILED -- {reason}");
            return new EffectFixtureRenderOutcome(
                plan.EffectName, plan.ResolutionTier, EffectFixtureRenderStatus.RenderFailed, reason,
                exportedXsqPath, renderResult.Artifact.FseqPath, FramesPath: null, FrameCount: 0,
                WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }

        if (renderResult.ProjectionWarnings.Count > 0)
        {
            // Never silent: a render can succeed structurally (a real .fseq was produced and decoded) while
            // RenderedFrameProjector still resolved zero illuminated points for every frame (e.g. a model
            // whose own StringType the projector does not recognize) -- that is a real, disclosed defect in
            // this fixture's own frames, not a cosmetic note, so it is logged even though Status still
            // reports Rendered (frames genuinely exist; they just may carry no real lit pixels).
            log?.Invoke($"{label}: PROJECTION WARNING(S) -- {string.Join(" | ", renderResult.ProjectionWarnings)}");
        }

        string framesPath;
        try
        {
            framesPath = Path.Combine(fixtureDirectory, "frames.json");
            await using var stream = File.Create(framesPath);
            await JsonSerializer.SerializeAsync(stream, renderResult.Frames, FramesJsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"{label}: DECODE/PERSIST FAILED -- {ex.Message}");
            return new EffectFixtureRenderOutcome(
                plan.EffectName, plan.ResolutionTier, EffectFixtureRenderStatus.DecodeFailed, ex.Message,
                exportedXsqPath, renderResult.Artifact.FseqPath, FramesPath: null, renderResult.Frames.Count,
                WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }

        stopwatch.Stop();
        log?.Invoke($"{label}: OK -- {renderResult.Frames.Count} frame(s) in {stopwatch.Elapsed.TotalSeconds:F1}s");
        return new EffectFixtureRenderOutcome(
            plan.EffectName, plan.ResolutionTier, EffectFixtureRenderStatus.Rendered, FailureReason: null,
            exportedXsqPath, renderResult.Artifact.FseqPath, framesPath, renderResult.Frames.Count,
            WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
    }

    private static string Sanitize(string effectName)
    {
        var chars = effectName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
        return new string(chars);
    }
}
