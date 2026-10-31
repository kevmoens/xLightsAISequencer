using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Refinement.Harness;

/// <summary>
/// Build-plan Phase 7b: one section's own declared intent for <see cref="SequenceMetricBattery"/> to
/// check measured behavior against -- deliberately plain primitives (a time window, a ceiling, a palette
/// band) rather than a direct reference to <c>XlightsAI.Studio.Director.ShowBible</c>/<c>MusicModel</c> or
/// <c>XlightsAI.Studio.Choreography.ChoreographyIR</c>, so <c>XlightsAI.Studio.Refinement</c> needs no new
/// project reference to those phases' own aggregates (mirrors <see cref="ChoreographyQualityContext"/>'s
/// own "every field independently optional, plain data" convention). The future Phase 7d orchestrator
/// extracts these from the real Show Bible/engine ceilings; this type never does that extraction itself.
/// </summary>
/// <param name="Window">This section's own time window, half-open <c>[Start,End)</c>.</param>
/// <param name="EnergyCeiling">The declared per-section energy ceiling in <c>[0,1]</c> (architecture §10's structural arc) this section's measured activity must never exceed.</param>
/// <param name="DeclaredPaletteSlotCount">How many distinct palette slots this section's Show Bible/Look declares active.</param>
/// <param name="StyleMinPaletteSlots">The configured style's own minimum allowed active palette slot count for a section.</param>
/// <param name="StyleMaxPaletteSlots">The configured style's own maximum allowed active palette slot count for a section.</param>
/// <param name="StyleMinChangeRatePerSecond">Build-plan Phase 8 task 1: the configured style's own minimum acceptable composition-change rate (mirrors <c>XlightsAI.Studio.Director.StyleDensity.MinChangeRatePerSecond</c> -- no project reference to Director exists here, per this type's own "plain primitives" convention) for this section, or <see langword="null"/> when no style density band applies.</param>
/// <param name="StyleMaxChangeRatePerSecond">The configured style's own maximum acceptable composition-change rate, or <see langword="null"/> when no style density band applies.</param>
public sealed record HarnessSection(
    TimeWindow Window,
    double EnergyCeiling,
    int DeclaredPaletteSlotCount,
    int StyleMinPaletteSlots,
    int StyleMaxPaletteSlots,
    double? StyleMinChangeRatePerSecond = null,
    double? StyleMaxChangeRatePerSecond = null);
