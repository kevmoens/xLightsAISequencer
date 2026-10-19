namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-05 Section 3's proposed signature, verbatim shape: one detected onset/transient candidate.
/// Pure algorithm output — never itself a user correction and never a <c>BeatMarker</c> (Section 2:
/// "Separate raw audio features from human-approved beat grid"); later steps (033-034) consume
/// <see cref="OnsetAnalysis.Onsets"/> to build tempo hypotheses and a <c>BeatGrid</c>, but this step's
/// own scope ends at the raw onset candidate list.
/// </summary>
/// <param name="TimeTicks">
/// Estimated onset position in canonical 100 ns ticks from the song's own sample index 0 (Section 2's
/// <c>TimelineTick</c> unit) — already group-delay corrected (Section 3 architectural design notes:
/// "correct algorithmic group delay"): this is the analysis window's *center* sample converted to
/// ticks, not the window's start, so the reported timestamp lands near the real transient rather than
/// systematically trailing it by half a window. See <see cref="OnsetDetector"/>'s own remarks for why
/// the window center is the correction applied.
/// </param>
/// <param name="Strength">Unit-normalized onset strength in <c>[0, 1]</c> — this run's spectral-flux peak divided by the single largest peak across the whole analysis (same "unit normalization" convention Step 031's <c>EnergyFrame.NormalizedEnergy</c> uses), never raw/unbounded flux.</param>
/// <param name="SourceAlgorithm">Identifies which onset-detection method produced this event (<see cref="OnsetDetector.AlgorithmName"/> for this step's own spectral-flux detector) — Section 3 architectural design notes name "optional percussive stem hooks" as a future alternative source this field is reserved for, not implemented by this step (disclosed known gap).</param>
public sealed record OnsetEvent(long TimeTicks, float Strength, string SourceAlgorithm);
