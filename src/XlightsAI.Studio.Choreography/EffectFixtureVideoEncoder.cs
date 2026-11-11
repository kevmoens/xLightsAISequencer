using System.Diagnostics;
using System.Text.Json;
using XlightsAI.Studio.Rendering;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 18 §4 (Phase C): "most effects are defined by their motion, which isolated still frames
/// lose. Build a real, literal video (or animated GIF) per effect/tier." Rasterizes **every** real decoded
/// <see cref="PreviewFrame"/> Phase B persisted (via <see cref="PreviewFrameRasterizer.Rasterize"/> -- the same
/// rasterizer <c>VisualCritic</c> already uses for stills) and encodes the full sequence into a real, playable
/// animated GIF (<see cref="AnimatedGifEncoder"/>) alongside the fixture's own <c>fixture.xsq</c>/<c>frames.json</c>,
/// never a silently skipped entry (Phase 18 §4's own acceptance criterion).
/// </summary>
public static class EffectFixtureVideoEncoder
{
    private const string VideoFileName = "video.gif";
    private const int RasterWidth = 320;
    private const int RasterHeight = 240;
    private const int PointRadiusPixels = 2;
    private const long TicksPerSecond = 10_000_000L;
    private const int DefaultDelayCentiseconds = 4; // 40ms/frame (25fps) fallback when two frames share a timestamp.

    private static readonly JsonSerializerOptions FramesJsonOptions = new() { WriteIndented = false };

    /// <param name="renderManifest">Phase B's own manifest -- every <see cref="EffectFixtureRenderOutcome"/> it lists is accounted for in the returned <see cref="EffectFixtureVideoManifest"/>, never silently dropped.</param>
    /// <param name="log">Optional progress sink (console + log-file convention, same as <c>tools/EffectCharacterizer</c> already uses).</param>
    public static async Task<EffectFixtureVideoManifest> EncodeAsync(
        EffectFixtureRenderManifest renderManifest, Action<string>? log, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(renderManifest);

        var outcomes = new List<EffectFixtureVideoOutcome>(renderManifest.Outcomes.Count);
        var fixtureIndex = 0;

        foreach (var renderOutcome in renderManifest.Outcomes)
        {
            fixtureIndex++;
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await EncodeOneAsync(renderOutcome, fixtureIndex, renderManifest.Outcomes.Count, log, ct).ConfigureAwait(false));
        }

        return new EffectFixtureVideoManifest(DateTimeOffset.UtcNow, outcomes);
    }

    private static async Task<EffectFixtureVideoOutcome> EncodeOneAsync(
        EffectFixtureRenderOutcome renderOutcome, int fixtureIndex, int fixtureCount, Action<string>? log, CancellationToken ct)
    {
        var label = $"[{fixtureIndex}/{fixtureCount}] {renderOutcome.EffectName} ({renderOutcome.ResolutionTier})";

        if (renderOutcome.Status != EffectFixtureRenderStatus.Rendered || renderOutcome.FramesPath is null)
        {
            var reason = $"Phase B never produced real frames for this fixture (status {renderOutcome.Status}): {renderOutcome.FailureReason}";
            log?.Invoke($"{label}: SKIPPED (no Phase B frames) -- {reason}");
            return new EffectFixtureVideoOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectFixtureVideoStatus.RenderSkipped, reason,
                VideoPath: null, FrameCount: 0, WallClockSeconds: 0);
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
            return new EffectFixtureVideoOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectFixtureVideoStatus.FramesReadFailed, ex.Message,
                VideoPath: null, FrameCount: 0, WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }

        try
        {
            var gifFrames = BuildGifFrames(frames);
            var gifBytes = AnimatedGifEncoder.Encode(RasterWidth, RasterHeight, gifFrames);

            var fixtureDirectory = Path.GetDirectoryName(renderOutcome.FramesPath)
                ?? throw new InvalidOperationException($"Could not determine the fixture directory from '{renderOutcome.FramesPath}'.");
            var videoPath = Path.Combine(fixtureDirectory, VideoFileName);
            await File.WriteAllBytesAsync(videoPath, gifBytes, ct).ConfigureAwait(false);

            stopwatch.Stop();
            log?.Invoke($"{label}: OK -- {frames.Count} frame(s), {gifBytes.Length:N0} byte GIF in {stopwatch.Elapsed.TotalSeconds:F1}s");
            return new EffectFixtureVideoOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectFixtureVideoStatus.Encoded, FailureReason: null,
                videoPath, frames.Count, WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log?.Invoke($"{label}: ENCODE FAILED -- {ex.Message}");
            return new EffectFixtureVideoOutcome(
                renderOutcome.EffectName, renderOutcome.ResolutionTier, EffectFixtureVideoStatus.EncodeFailed, ex.Message,
                VideoPath: null, frames.Count, WallClockSeconds: stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>
    /// Rasterizes every frame and pairs it with a real, measured delay derived from consecutive frames' own
    /// <see cref="PreviewFrame.TimeTicks"/> (the same 100ns tick unit <see cref="EffectFixturePlanner"/> already
    /// uses) -- never a single guessed constant applied to every frame regardless of its actual real spacing.
    /// </summary>
    private static List<AnimatedGifEncoder.Frame> BuildGifFrames(IReadOnlyList<PreviewFrame> frames)
    {
        var gifFrames = new List<AnimatedGifEncoder.Frame>(frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            var rgb = PreviewFrameRasterizer.Rasterize(frames[i], RasterWidth, RasterHeight, PointRadiusPixels);
            var delayCentiseconds = i + 1 < frames.Count
                ? TicksToDelayCentiseconds(frames[i + 1].TimeTicks - frames[i].TimeTicks)
                : (i > 0 ? TicksToDelayCentiseconds(frames[i].TimeTicks - frames[i - 1].TimeTicks) : DefaultDelayCentiseconds);
            gifFrames.Add(new AnimatedGifEncoder.Frame(rgb, delayCentiseconds));
        }

        return gifFrames;
    }

    private static int TicksToDelayCentiseconds(long deltaTicks)
    {
        if (deltaTicks <= 0)
        {
            return DefaultDelayCentiseconds;
        }

        var centiseconds = (int)Math.Round(deltaTicks * 100.0 / TicksPerSecond);
        return Math.Max(1, centiseconds);
    }
}
