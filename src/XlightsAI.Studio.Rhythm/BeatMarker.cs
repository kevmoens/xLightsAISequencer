namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Section 3's proposed signature, with one disclosed addition: one generated or
/// user-corrected beat position. <see cref="BeatWithinBar"/> <c>== 0</c> is this beat's likely
/// downbeat (Step 034's own title: "Generate beats, likely downbeats, bars and confidence
/// intervals") — a simple, disclosed convention (the first beat of every bar is always the
/// downbeat), not an independent accent-detection pass; Section 3 explicitly scopes downbeat
/// estimation as "optional... with confidence and manual labeling," and a real user correction
/// entry point is Step 035's own scope, not this step's.
/// </summary>
/// <param name="Id">Stable identity for this one marker — required so a later step (035) can address/move/delete a single beat without re-identifying it by position.</param>
/// <param name="TimeTicks">Absolute position in canonical 100 ns ticks from the song's own sample index 0 (Section 2's <c>TimelineTick</c> unit) — never re-based to a bar-relative offset.</param>
/// <param name="BeatWithinBar">Zero-based beat position inside its bar, in <c>[0, BeatGridSettings.BeatsPerBar)</c>. <c>0</c> is the bar's likely downbeat.</param>
/// <param name="BarIndex">Zero-based bar number this beat belongs to — Step 034's own addition beyond Section 3's proposed shape ("Represent beats with absolute ticks and bar indices," Step 034 implementation guidance, verbatim); Section 3's signatures are explicitly "proposed API intent," not a verified, unmodifiable shape (Section 1).</param>
/// <param name="Confidence">Unit-normalized confidence in <c>[0, 1]</c> that a real rhythmic event actually occurs at <see cref="TimeTicks"/> — lower for a beat inferred purely from the chosen tempo period with no corroborating onset nearby, never fabricated as uniformly certain (Step 034's own implementation guidance: "show confidence and avoid asserting uncertainty away").</param>
/// <param name="UserCorrected">Whether a human has explicitly accepted or moved this specific beat (Step 035's own scope) — always <see langword="false"/> for every beat this step (034) generates.</param>
public sealed record BeatMarker(
    Guid Id,
    long TimeTicks,
    int BeatWithinBar,
    int BarIndex,
    double Confidence,
    bool UserCorrected);
