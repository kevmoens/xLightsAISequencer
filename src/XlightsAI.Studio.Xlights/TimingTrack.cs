namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1c (architecture §6): one named timing track (e.g. <c>Beats</c>, <c>Bars</c>, <c>Phrases</c>,
/// <c>Sections</c>, a labelled <c>Percussion</c>) exported as its own
/// <c>&lt;Element type="timing" name="..."&gt;</c> — real xLights structure, verified against
/// <c>C:\xLights</c>'s own sequences (docs/PHASE0-FINDINGS.md's own exporter ground truth work).
/// Labelling marks (<see cref="TimingMark.Label"/>) is what architecture §6 says "unlocks the whole
/// filter mechanism" for timing-track-bound effects (Shockwave/Ripple/Fireworks/etc.).
/// </summary>
public sealed record TimingTrack(string Name, IReadOnlyList<TimingMark> Marks);
