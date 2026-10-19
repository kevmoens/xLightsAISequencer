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
/// <param name="MeterGuess">A simple, disclosed-as-approximate meter label (e.g. <c>"4/4"</c>) — Section 3 names downbeat/meter estimation as "optional... with confidence and manual labeling," not a solved problem this step claims to fully answer.</param>
public sealed record TempoHypothesis(double Bpm, double Confidence, string MeterGuess);
