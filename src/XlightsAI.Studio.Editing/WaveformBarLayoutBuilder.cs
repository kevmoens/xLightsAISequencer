using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-04 Step 027 ("Render synchronized waveform beneath ruler"): turns a
/// <see cref="WaveformPyramidCache"/> (Step 026) into the pixel-space bars a timeline control
/// renders beneath its ruler, through the exact same <see cref="TimelineViewport"/> transform
/// <see cref="TimelineRulerTickGenerator"/> and <see cref="EffectBlockLayoutBuilder"/> already map
/// through — "Render sample-index-aligned waveform in existing timeline coordinate system" (this
/// step's implementation guidance). Only the currently visible, virtualized sample range is ever
/// converted to bars — never the full pyramid — the same virtualization discipline every other
/// timeline-aware builder in this project already follows.
/// </summary>
public static class WaveformBarLayoutBuilder
{
    /// <summary>100 ns ticks/s, the canonical unit conversion factor (PHASE-04 Section 2) — same constant <see cref="XlightsAI.Studio.Audio.SongAsset"/> and <c>WaveOutAudioTransport</c> use.</summary>
    private const long TicksPerSecond = 10_000_000L;

    /// <summary>
    /// Builds one <see cref="WaveformBarLayout"/> per envelope (at the resolution level best
    /// matching <paramref name="viewport"/>'s current zoom) that overlaps the currently visible
    /// window across <paramref name="viewportWidthPixels"/> pixels. Explicitly handled edge cases
    /// (never an exception, never garbage output — this step's own "Silence, invalid timing
    /// boundaries and missing media must be handled explicitly" requirement):
    /// <list type="bullet">
    /// <item>A cache with no resolution levels, a non-positive sample rate, or a non-positive
    /// sample count (an "invalid timing boundary" — nothing meaningful can be rendered) returns an
    /// empty list rather than dividing by zero or indexing out of range.</item>
    /// <item>True silence (every sample zero) is not special-cased at all: its real,
    /// correctly-computed <c>Min=Max=Rms=0</c> envelopes still produce real, correctly positioned
    /// bars — "handled explicitly" by simply not crashing or inventing placeholder data, the same
    /// convention <see cref="WaveformPyramidBuilder"/> itself already established.</item>
    /// <item>"Missing media" is not this builder's own concern — a <see langword="null"/>
    /// <paramref name="cache"/> (no waveform built/loaded yet) is the caller's responsibility to
    /// check before calling this method at all, the same "missing media" convention
    /// <see cref="TimelineRulerTickGenerator"/>'s own caller (a <see langword="null"/>
    /// <c>Viewport</c>) already follows.</item>
    /// </list>
    /// </summary>
    /// <param name="cache">The multiresolution waveform pyramid to render.</param>
    /// <param name="viewport">The shared timeline viewport every other timeline-aware control maps through.</param>
    /// <param name="viewportWidthPixels">The real pixel width of the waveform strip, same contract as <see cref="TimelineRulerTickGenerator.GenerateTicks"/>.</param>
    /// <param name="originTicks">The canonical tick that this cache's sample index 0 maps to (<see cref="XlightsAI.Studio.Audio.SongAsset.OriginTicks"/>); defaults to 0 for a freshly probed, not-yet-re-origined song.</param>
    public static IReadOnlyList<WaveformBarLayout> BuildBars(
        WaveformPyramidCache cache, TimelineViewport viewport, double viewportWidthPixels, long originTicks = 0)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(viewport);

        if (cache.SourceSampleRate <= 0 || cache.SampleCount <= 0 || cache.Resolutions.Length == 0)
        {
            return [];
        }

        var level = SelectResolutionLevel(cache, viewport.TicksPerPixel);
        if (level.Envelopes.Length == 0)
        {
            return [];
        }

        var visible = viewport.VisibleWindow(viewportWidthPixels);
        var bars = new List<WaveformBarLayout>(level.Envelopes.Length);

        foreach (var envelope in level.Envelopes)
        {
            var startTicks = originTicks + SamplesToTicks(envelope.StartSample, cache.SourceSampleRate);
            var endTicks = originTicks + SamplesToTicks(envelope.EndSample, cache.SourceSampleRate);

            // Half-open overlap test against the visible window (Section 2: "All time scopes are
            // half-open [start,end)") — only envelopes actually on screen are ever converted,
            // keeping this virtualized exactly like TimelineRulerTickGenerator/EffectBlockLayoutBuilder.
            if (endTicks <= visible.StartTicks || startTicks >= visible.EndTicks)
            {
                continue;
            }

            var x = viewport.XFromTime(startTicks);
            var width = Math.Max(0d, viewport.XFromTime(endTicks) - x);
            bars.Add(new WaveformBarLayout(x, width, envelope.Min, envelope.Max, envelope.Rms));
        }

        return bars;
    }

    /// <summary>
    /// Chooses the finest (smallest <see cref="WaveformResolutionLevel.BlockSizeSamples"/>) level
    /// whose per-block sample span covers at least one on-screen pixel at the current zoom — never
    /// rendering more envelopes than pixels (overdraw), while staying as detailed as the current
    /// zoom actually allows. Falls back to the coarsest level when even it spans less than a pixel
    /// (fully zoomed out), since there is no finer alternative that would do better.
    /// </summary>
    private static WaveformResolutionLevel SelectResolutionLevel(WaveformPyramidCache cache, double ticksPerPixel)
    {
        var samplesPerPixel = ticksPerPixel * cache.SourceSampleRate / TicksPerSecond;

        WaveformResolutionLevel? best = null;
        foreach (var level in cache.Resolutions.OrderBy(r => r.BlockSizeSamples))
        {
            best = level;
            if (level.BlockSizeSamples >= samplesPerPixel)
            {
                return level;
            }
        }

        return best!;
    }

    private static long SamplesToTicks(long samples, int sampleRate) =>
        sampleRate <= 0 ? 0 : (long)Math.Round(samples * (TicksPerSecond / (double)sampleRate));
}
