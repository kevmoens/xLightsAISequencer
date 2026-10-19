namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 047's own "tunable thresholds" (implementation guidance, verbatim: "Transform note
/// differences to repeat/step/leap/hold/rest with tunable thresholds; measure musical interval not only
/// note index"). Not part of any persisted artifact's own cache key -- unlike
/// <see cref="PitchTrackingSettings"/>, <see cref="MelodicContourClassifier"/> is a pure, synchronous,
/// re-runnable domain transform (same shape as <see cref="MelodyTrackValidator"/>), so there is no cache
/// to invalidate; a caller simply re-classifies with different settings whenever it chooses.
/// </summary>
/// <param name="StepThresholdSemitones">
/// The largest absolute signed-semitone interval (Section 3: "measure musical interval not only note
/// index" -- always <see cref="NoteEvent.MidiNote"/> difference, never list-index difference) still
/// classified as <see cref="PitchMotion.StepUp"/>/<see cref="PitchMotion.StepDown"/>; anything larger is
/// <see cref="PitchMotion.LeapUp"/>/<see cref="PitchMotion.LeapDown"/>. Must be positive.
/// </param>
/// <param name="RestGapTicks">
/// The minimum silent gap, in canonical 100 ns ticks, between one note's <see cref="NoteEvent.EndTicks"/>
/// and the next note's <see cref="NoteEvent.StartTicks"/>, for the arriving note to be classified
/// <see cref="PitchMotion.Rest"/> instead of a pitch-based motion -- Section 3: "Represent rests
/// explicitly." Must be positive.
/// </param>
/// <param name="MinNoteConfidence">
/// This step's own named "noise filtering": a note whose <see cref="NoteEvent.Confidence"/> falls below
/// this threshold still receives its own <see cref="MelodicGesture"/> (classified against the last
/// *confident* note, never silently dropped), but is itself skipped as the reference point for every
/// later note's own interval -- so a single noisy/ornamental blip cannot corrupt the motion reported for
/// the real note that follows it. Must be in <c>[0, 1]</c>.
/// </param>
public sealed record GestureClassificationSettings(
    int StepThresholdSemitones,
    long RestGapTicks,
    double MinNoteConfidence)
{
    /// <summary>
    /// A musically reasonable default, verified directly against Step 045's own already-committed
    /// C4-D4-E4-G4-E4-D4-C4 fixture (<c>Step045AcceptanceTests.Step045_Acceptance_Passes_...</c>): a whole
    /// step (2 semitones) classifies as Step, a minor third (3 semitones) classifies as Leap; a
    /// 100,000-tick (0.01s) phrase gap between consecutive real notes is ordinary legato spacing, not a
    /// rest; a 1,000,000-tick (0.1s) or larger gap is a real musical rest.
    /// </summary>
    public static GestureClassificationSettings Default { get; } = new(
        StepThresholdSemitones: 2,
        RestGapTicks: 1_000_000L,
        MinNoteConfidence: 0.5);
}
