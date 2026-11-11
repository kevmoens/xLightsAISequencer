using System.Diagnostics;
using System.Text.Json;
using XlightsAI.Studio.Choreography;
using XlightsAI.Studio.Rendering;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 18 §5 (Phase D): "turn each real video/frame sequence into a concise, structured,
/// reusable text description of what the effect actually does."
///
/// <para>
/// <b>Resolved open question (verified against every real client this app has, not assumed either way):</b>
/// none of <see cref="AnthropicChatCompletionClient"/>/<see cref="OpenAiResponsesChatCompletionClient"/>/
/// <see cref="OpenAiCompatibleChatCompletionClient"/> can accept a video or animated GIF -- every one of
/// them hardcodes <c>image/png</c> (or a <c>data:image/png;base64,...</c> URL) at the wire-protocol level for
/// every entry in <see cref="MultimodalChatCompletionRequest.ImagesPng"/>. So per the build plan's own
/// disclosed fallback, this submits a small, evenly-spaced set of representative PNG stills
/// (<see cref="StillCount"/>) sampled from Phase B's own real decoded frame sequence -- never the whole
/// video -- in one multimodal call (the same request shape already supports multiple images).
/// </para>
///
/// <para>
/// <b>Disclosed heuristic, not ground truth</b> -- same discipline this codebase already applies to
/// <c>VisemeApproximator</c>: a single vision-model pass over a handful of stills can mischaracterize a
/// subtle effect, and Phase 18 §5's own acceptance criteria require a human spot-check before this catalog
/// is trusted for Phase E.
/// </para>
/// </summary>
public static class EffectVisualCharacterizer
{
    /// <summary>
    /// Six evenly-spaced stills (t=0, 0.2, 0.4, 0.6, 0.8, 1.0 of the real decoded frame sequence) -- enough
    /// to show one full motion cycle for a periodic effect over <see cref="EffectFixturePlanner.DefaultFixtureDurationSeconds"/>
    /// without the token/cost blowup of sending every one of 240 decoded frames; a disclosed sampling choice,
    /// not a claim that 6 stills can never miss a transient.
    /// </summary>
    private const int StillCount = 6;

    private const int RasterWidth = 320;
    private const int RasterHeight = 240;

    /// <summary>
    /// Smaller than <see cref="VisualCritic.FrameRasterPointRadiusPixels"/> (14): that radius is tuned for a
    /// real full-house judging call where overall colour mass matters more than individual point position,
    /// but Phase 18 §4's own per-node spatial fan-out (<see cref="Rendering.RenderedFrameProjector"/>) is the
    /// one new signal this phase needs to actually read -- a large radius would blur adjacent nodes back
    /// into the same colour mass this phase exists to see past.
    /// </summary>
    private const int PointRadiusPixels = 3;

    /// <summary>
    /// Deliberately generous -- real-run finding: a reasoning-capable Responses API model's hidden reasoning
    /// tokens are deducted from <c>max_output_tokens</c> before any visible answer text is emitted
    /// (<see cref="OpenAiResponsesChatCompletionClient"/>'s own doc comment already discloses this exact
    /// mechanism). A first real sweep at 300 tokens left several of the 6-image calls with zero budget
    /// remaining for the actual JSON answer ("no output[].content[].text", or a JSON value cut off
    /// mid-string) -- this is a heavier per-call task than <see cref="VisualCritic"/>'s single pairwise A/B
    /// pick (<see cref="VisualCritic.IssueAsync"/> budgets only 100), since six images get analyzed at once.
    /// </summary>
    private const int MaxOutputTokens = 2000;

    private static readonly JsonSerializerOptions FramesJsonOptions = new() { WriteIndented = false };

    /// <param name="renderManifest">Phase B's own manifest -- every <see cref="EffectFixtureRenderOutcome"/> it lists is accounted for in the returned <see cref="EffectVisualCharacterizationManifest"/>, never silently dropped.</param>
    /// <param name="client">The real, already-resolved vision-capable client for the <see cref="AiRole.Critic"/> role (same resolution path <c>AiChoreographyWholeSongGenerator</c>'s own Quality Pass wiring already uses) -- never a second client-construction path.</param>
    /// <param name="modelName">The same role's configured model name, persisted alongside every outcome for provenance.</param>
    /// <param name="log">Optional progress sink (console + log-file convention, same as <c>tools/EffectCharacterizer</c> already uses).</param>
    public static async Task<EffectVisualCharacterizationManifest> CharacterizeAsync(
        EffectFixtureRenderManifest renderManifest, IVisionCapableChatCompletionClient client, string modelName,
        Action<string>? log, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(renderManifest);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        var outcomes = new List<EffectVisualCharacterizationOutcome>(renderManifest.Outcomes.Count);
        var fixtureIndex = 0;

        foreach (var renderOutcome in renderManifest.Outcomes)
        {
            fixtureIndex++;
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await CharacterizeOneAsync(
                renderOutcome, fixtureIndex, renderManifest.Outcomes.Count, client, modelName, log, ct).ConfigureAwait(false));
        }

        return new EffectVisualCharacterizationManifest(DateTimeOffset.UtcNow, modelName, outcomes);
    }

    private static async Task<EffectVisualCharacterizationOutcome> CharacterizeOneAsync(
        EffectFixtureRenderOutcome renderOutcome, int fixtureIndex, int fixtureCount,
        IVisionCapableChatCompletionClient client, string modelName, Action<string>? log, CancellationToken ct)
    {
        var label = $"[{fixtureIndex}/{fixtureCount}] {renderOutcome.EffectName} ({renderOutcome.ResolutionTier})";

        if (renderOutcome.Status != EffectFixtureRenderStatus.Rendered || renderOutcome.FramesPath is null)
        {
            var reason = $"Phase B never produced real frames for this fixture (status {renderOutcome.Status}): {renderOutcome.FailureReason}";
            log?.Invoke($"{label}: SKIPPED (no Phase B frames) -- {reason}");
            return new EffectVisualCharacterizationOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectVisualCharacterizationStatus.RenderSkipped, reason,
                Description: null, WallClockSeconds: 0);
        }

        var stopwatch = Stopwatch.StartNew();

        List<PreviewFrame>? frames;
        try
        {
            await using var stream = File.OpenRead(renderOutcome.FramesPath);
            frames = await JsonSerializer.DeserializeAsync<List<PreviewFrame>>(stream, FramesJsonOptions, ct).ConfigureAwait(false);
            if (frames is null || frames.Count == 0)
            {
                throw new InvalidOperationException($"'{renderOutcome.FramesPath}' deserialized to no frames.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"{label}: FRAMES READ FAILED -- {ex.Message}");
            return new EffectVisualCharacterizationOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectVisualCharacterizationStatus.FramesReadFailed, ex.Message,
                Description: null, WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }

        // Build-plan Phase 21 §2a: computed from these SAME real frames, before the AI call, and persisted
        // regardless of whether that AI call below succeeds -- the measurement spends no AI budget and needs
        // nothing beyond what Phase B already decoded, so a later AI failure must never also lose this.
        var totalPixelCount = TotalPixelCount(renderOutcome.ResolutionTier);
        var (meanBrightness, peakBrightness) = EffectFrameBrightnessMeasurer.Measure(frames, totalPixelCount);
        log?.Invoke($"{label}: measured MeanBrightness={meanBrightness:F4}, PeakBrightness={peakBrightness:F4} (whole-canvas, {totalPixelCount}px).");

        try
        {
            var stills = SampleStills(frames);
            var description = await RequestDescriptionAsync(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, stills, client, modelName, ct).ConfigureAwait(false);

            stopwatch.Stop();
            log?.Invoke($"{label}: OK -- \"{description.Summary}\" in {stopwatch.Elapsed.TotalSeconds:F1}s");
            return new EffectVisualCharacterizationOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectVisualCharacterizationStatus.Characterized, FailureReason: null,
                description, WallClockSeconds: stopwatch.Elapsed.TotalSeconds, meanBrightness, peakBrightness);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"{label}: AI CALL FAILED -- {ex.Message}");
            return new EffectVisualCharacterizationOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectVisualCharacterizationStatus.AiCallFailed, ex.Message,
                Description: null, WallClockSeconds: stopwatch.Elapsed.TotalSeconds, meanBrightness, peakBrightness);
        }
    }

    /// <summary>
    /// Build-plan Phase 21 §2a/§2c: augments an already-completed Phase D sweep with real
    /// <see cref="EffectVisualCharacterizationOutcome.MeanBrightness"/>/<see cref="EffectVisualCharacterizationOutcome.PeakBrightness"/>
    /// values, reading the exact same persisted <see cref="EffectFixtureRenderOutcome.FramesPath"/> frames a
    /// prior <see cref="CharacterizeAsync"/> run already decoded -- never a second AI call, never a
    /// re-render. Every other field of each outcome (<see cref="EffectVisualCharacterizationOutcome.Description"/>
    /// included) passes through unchanged; this exists so a committed catalog can gain brightness without
    /// re-spending the real, billed AI calls its Description text already cost.
    /// </summary>
    /// <param name="existingManifest">A prior real <see cref="CharacterizeAsync"/> result.</param>
    /// <param name="renderManifest">The Phase B render manifest <paramref name="existingManifest"/> was built from -- supplies each fixture's own <see cref="EffectFixtureRenderOutcome.FramesPath"/>.</param>
    public static async Task<EffectVisualCharacterizationManifest> RecomputeBrightnessAsync(
        EffectVisualCharacterizationManifest existingManifest, EffectFixtureRenderManifest renderManifest, Action<string>? log, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(existingManifest);
        ArgumentNullException.ThrowIfNull(renderManifest);

        var renderByKey = renderManifest.Outcomes.ToDictionary(o => (o.EffectName, o.ResolutionTier));
        var outcomes = new List<EffectVisualCharacterizationOutcome>(existingManifest.Outcomes.Count);

        foreach (var outcome in existingManifest.Outcomes)
        {
            var label = $"{outcome.EffectName} ({outcome.ResolutionTier})";

            if (!renderByKey.TryGetValue((outcome.EffectName, outcome.ResolutionTier), out var renderOutcome)
                || renderOutcome.Status != EffectFixtureRenderStatus.Rendered || renderOutcome.FramesPath is null)
            {
                log?.Invoke($"{label}: no real Phase B frames available -- brightness left unmeasured.");
                outcomes.Add(outcome);
                continue;
            }

            List<PreviewFrame>? frames;
            try
            {
                await using var stream = File.OpenRead(renderOutcome.FramesPath);
                frames = await JsonSerializer.DeserializeAsync<List<PreviewFrame>>(stream, FramesJsonOptions, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log?.Invoke($"{label}: FRAMES READ FAILED -- {ex.Message} -- brightness left unmeasured.");
                outcomes.Add(outcome);
                continue;
            }

            if (frames is null || frames.Count == 0)
            {
                log?.Invoke($"{label}: '{renderOutcome.FramesPath}' deserialized to no frames -- brightness left unmeasured.");
                outcomes.Add(outcome);
                continue;
            }

            var totalPixelCount = TotalPixelCount(renderOutcome.ResolutionTier);
            var (meanBrightness, peakBrightness) = EffectFrameBrightnessMeasurer.Measure(frames, totalPixelCount);
            log?.Invoke($"{label}: measured MeanBrightness={meanBrightness:F4}, PeakBrightness={peakBrightness:F4} (whole-canvas, {totalPixelCount}px).");

            outcomes.Add(outcome with { MeanBrightness = meanBrightness, PeakBrightness = peakBrightness });
        }

        return existingManifest with { Outcomes = outcomes };
    }

    /// <summary>The fixture's own total addressable pixel count by tier -- <see cref="EffectFixturePlanner.LowResolutionModel"/>/<see cref="EffectFixturePlanner.HighResolutionModel"/>'s own real <c>NodeCount</c>, never a guessed canvas size.</summary>
    private static int TotalPixelCount(EffectFixtureResolutionTier tier) => tier switch
    {
        EffectFixtureResolutionTier.Low => EffectFixturePlanner.LowResolutionModel.NodeCount,
        EffectFixtureResolutionTier.High => EffectFixturePlanner.HighResolutionModel.NodeCount,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown resolution tier."),
    };

    private static List<byte[]> SampleStills(IReadOnlyList<PreviewFrame> frames)
    {
        var stills = new List<byte[]>(StillCount);
        for (var i = 0; i < StillCount; i++)
        {
            var fraction = StillCount <= 1 ? 0.0 : (double)i / (StillCount - 1);
            var frameIndex = Math.Min(frames.Count - 1, (int)Math.Round(fraction * (frames.Count - 1)));
            stills.Add(PreviewFrameRasterizer.RasterizeToPng(frames[frameIndex], RasterWidth, RasterHeight, PointRadiusPixels));
        }

        return stills;
    }

    private static async Task<EffectVisualCharacterizationDescription> RequestDescriptionAsync(
        string effectName, EffectFixtureResolutionTier tier, List<byte[]> stills,
        IVisionCapableChatCompletionClient client, string modelName, CancellationToken ct)
    {
        const string systemPrompt =
            "You are a lighting-design analyst characterizing one real, isolated xLights effect. It was " +
            "rendered on its own fixture with nothing else active, so every illuminated dot you see belongs " +
            "to this one effect alone -- never a guess about a busier real show.";

        var nodeDescription = tier == EffectFixtureResolutionTier.Low
            ? "a low-resolution string prop (20 pixels)"
            : "a high-resolution matrix-like line (900 pixels)";

        var userPrompt =
            $"These {stills.Count} images are evenly-spaced stills, in temporal order, sampled across one " +
            $"real, looping render of the xLights effect named \"{effectName}\" applied to {nodeDescription}. " +
            "Each pixel is drawn as a small dot at its own real position and colour on a black background. " +
            "Describe what this effect actually does across these stills: its motion type, direction (if " +
            "any), apparent speed, and texture/density impression. " +
            "Respond with ONLY a JSON object of this exact shape: {\"motionType\":\"chase|wipe|pulse|twinkle|" +
            "static-hold|other\",\"direction\":\"left-to-right|right-to-left|none|other\",\"apparentSpeed\":" +
            "\"slow|medium|fast|none\",\"textureDensity\":\"sparse|moderate|dense\",\"summary\":\"one plain-" +
            "language sentence suitable for a lighting designer\"}. No other text.";

        var request = new MultimodalChatCompletionRequest(systemPrompt, userPrompt, stills, CacheSystemPrompt: true, MaxOutputTokens: MaxOutputTokens);
        var result = await client.CompleteAsync(request, ct).ConfigureAwait(false);

        var json = StructuredJsonExtractor.ExtractJson(result.Text);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string Require(string field) =>
            root.TryGetProperty(field, out var node) && node.GetString() is { Length: > 0 } text
                ? text
                : throw new AiCallRejectedException(AiCallRejectionReason.MalformedJson, $"Visual characterization response was missing a non-empty '{field}' field.");

        return new EffectVisualCharacterizationDescription(
            Require("motionType"), Require("direction"), Require("apparentSpeed"), Require("textureDensity"), Require("summary"));
    }
}
