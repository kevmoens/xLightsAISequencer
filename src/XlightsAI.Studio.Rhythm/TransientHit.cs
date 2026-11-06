namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Build-plan "richer analysis tools" Phase 2 (2026-11-02): one loud, percussive transient --
/// a candidate "hit" moment (a bang, a drum hit, a sudden sub-bass impact), as opposed to a general
/// onset (which also fires on quieter melodic note starts). Produced by <see cref="TransientHitDetector"/>
/// as a pure post-filter over <see cref="OnsetAnalysis"/> -- no new signal processing, just a stricter
/// threshold on data this codebase already computes.
/// </summary>
/// <param name="TimeTicks">The underlying <see cref="OnsetEvent.TimeTicks"/> this hit was promoted from.</param>
/// <param name="Strength">The underlying <see cref="OnsetEvent.Strength"/> (already normalized to this song's own run, <c>[0,1]</c>).</param>
public sealed record TransientHit(long TimeTicks, float Strength);
