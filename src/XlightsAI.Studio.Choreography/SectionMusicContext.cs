using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>The real analysis data one section's <see cref="LookCompiler.Compile"/> call is built from -- a narrower, section-scoped view of the whole song's <c>MusicModel</c> (architecture §3's L0 output).</summary>
/// <param name="Window">This section's own half-open tick window.</param>
/// <param name="Kind">The classified section kind, or <see cref="SectionKind.Unknown"/>.</param>
/// <param name="EnergyCeiling">This section's declared energy ceiling in <c>[0,1]</c> (architecture §10's structural arc) -- never exceeded by a compiled Look whose own <see cref="Look.EnergyCeiling"/> is higher.</param>
/// <param name="BarGrid">The whole song's bar grid (not re-sliced per section -- bar/beat indices stay song-global, per Phase 4 task 6's own unit discipline).</param>
/// <param name="TimingTracksByName">The whole song's own emitted timing tracks (<see cref="TimingTrackEmitter"/>), keyed by name -- shared across every section's compile (a timing track is song-spanning, never duplicated per section).</param>
/// <param name="VocalActive">Whether vocals are judged active for the majority of this section's own window (architecture §10: "vocalActive gating hold-vs-chase").</param>
/// <param name="TargetChangeRatePerSecond">
/// Build-plan Phase 12 §3a: this section's own measured target change-rate (effect starts per second),
/// derived from <paramref name="EnergyCeiling"/> via <see cref="SectionDensityMapper"/> -- a section-level
/// ceiling <see cref="LookCompiler"/>'s own Mode B cadence is throttled toward, on top of (never replacing)
/// each layer's own declared <see cref="LookLayer.Duty"/>/<see cref="LookLayer.Timescale"/> choice.
/// Defaults to <see cref="double.PositiveInfinity"/> ("no additional throttling") so every caller that
/// predates this phase (including every existing direct <see cref="SectionMusicContext"/> construction in
/// tests) keeps compiling identically unchanged.
/// </param>
public sealed record SectionMusicContext(
    TimeWindow Window,
    SectionKind Kind,
    double EnergyCeiling,
    BarGrid BarGrid,
    IReadOnlyDictionary<string, TimingTrack> TimingTracksByName,
    bool VocalActive,
    double TargetChangeRatePerSecond = double.PositiveInfinity)
{
    /// <summary>
    /// Build-plan Phase 8 task 2: the single, shared "vocals active for the majority of this window" rule
    /// -- extracted from <c>ChoreographyEngine.Compile</c>'s own former private implementation so
    /// <c>ChoreographyGenerationPipeline.GenerateSectionScriptsAsync</c> can compute the real value instead
    /// of hardcoding <see langword="false"/> (the bug this task fixes: the Choreographer prompt never saw
    /// real vocal data even though <see cref="SectionScriptPromptBuilder"/> already renders it).
    /// </summary>
    public static bool IsVocalActiveMajority(VocalActivityEnvelope? vocalActivity, TimeWindow window)
    {
        if (vocalActivity is null)
        {
            return false;
        }

        var activeTicks = vocalActivity.ActiveRegions.Sum(r =>
            Math.Max(0, Math.Min(r.EndTicks, window.EndTicks) - Math.Max(r.StartTicks, window.StartTicks)));
        var windowTicks = window.EndTicks - window.StartTicks;

        return windowTicks > 0 && activeTicks > windowTicks / 2.0;
    }
}
