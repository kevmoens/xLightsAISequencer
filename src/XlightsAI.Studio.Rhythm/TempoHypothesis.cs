namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Section 3's proposed signature, verbatim shape: one ranked tempo candidate. Step 033's own
/// required acceptance ("Rank multiple tempo candidates rather than one BPM") means a single
/// <see cref="TempoAnalysis"/> always carries a list of these, ordered by <see cref="Confidence"/>
/// descending — never a single collapsed "the" BPM. Deliberately includes half/double-time alternates of
/// the same underlying periodicity (Section 3 architectural design notes: "Compare multiple tempo
/// candidates (e.g. double/half-time)"), not just the single strongest peak.
/// </summary>
/// <param name="Bpm">Beats per minute this candidate represents.</param>
/// <param name="Confidence">Unit-normalized periodicity strength in <c>[0, 1]</c> — this candidate's own autocorrelation score divided by the single largest score found across the whole scanned BPM range, same "unit normalization" convention <see cref="OnsetEvent.Strength"/> and <c>EnergyFrame.NormalizedEnergy</c> already use.</param>
/// <param name="MeterGuess">A meter label (e.g. <c>"4/4"</c>) chosen by real downbeat/offbeat onset-strength comparison (PHASE-28 §3.1) — never a bare, undisclosed guess; see <see cref="MeterConfidence"/> for how certain this particular label is.</param>
/// <param name="MeterConfidence">
/// PHASE-28 §3.1 — unit-normalized confidence, in <c>[0, 1]</c>, that <see cref="MeterGuess"/> is the real
/// meter: how much the winning meter's downbeat/offbeat onset-strength ratio exceeds "no distinction at
/// all" (ratio of exactly 1). <c>0</c> when no candidate meter could be distinguished from the others at
/// all (e.g. a perfectly isochronous, unaccented click train) — still a real, disclosed label plus an
/// honest confidence, never a silently-assumed meter.
/// </param>
public sealed record TempoHypothesis(double Bpm, double Confidence, string MeterGuess, double MeterConfidence = 0.0);
