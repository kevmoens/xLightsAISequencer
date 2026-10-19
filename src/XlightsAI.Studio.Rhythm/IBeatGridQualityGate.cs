namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 037's own quality/diagnostics boundary over an already-built/corrected <see cref="BeatGrid"/>
/// (Steps 034/035) — a pure, synchronous, in-memory domain function over a value already in hand, the
/// same deliberate narrowing precedent <see cref="IBeatGridEditor"/>/<see cref="IRhythmGesturePlanner"/>
/// already set relative to this phase's own Section 3 proposals (Section 3 itself proposes no explicit
/// confidence/quality-gate interface for this step; Step 037's own required acceptance and
/// implementation guidance are the source of truth here).
/// </summary>
public interface IBeatGridQualityGate
{
    /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="settings"/> is <see langword="null"/>.</exception>
    /// <exception cref="BeatGridQualityGateInvalidException"><paramref name="settings"/>'s threshold/bucket size is not usable.</exception>
    BeatGridQualityReport Evaluate(BeatGrid grid, BeatGridQualityGateSettings settings);
}
