namespace XlightsAI.Studio.Pipeline;

/// <summary>PHASE-2b (build plan §2b.9): one retrievable exemplar — a musical fingerprint plus enough of its aligned effect data to reconstruct "what the lights did" for that window (architecture §8b, verbatim).</summary>
/// <param name="Fingerprint">The window's musical fingerprint.</param>
/// <param name="Effects">Every aligned effect whose start bar falls inside this window's <c>[StartBarIndex, EndBarIndexExclusive)</c> range, ordered by start time.</param>
public sealed record ExemplarEntry(ExemplarFingerprint Fingerprint, IReadOnlyList<AlignedEffectInstance> Effects);
