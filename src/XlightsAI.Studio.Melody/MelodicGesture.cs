namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Section 3's own proposed signature, verbatim: the relative-motion abstraction derived from
/// a pair of notes (Step 047's own scope), always traceable back to the concrete <see cref="NoteEvent"/>
/// it describes via <see cref="NoteId"/> -- never a copy of the note's own absolute pitch/timing.
/// <see cref="IntervalSemitones"/> is signed musical distance (not note-index distance -- Section 3:
/// "measure musical interval not only note index"); <c>0</c> for <see cref="PitchMotion.Repeat"/>,
/// <see cref="PitchMotion.Hold"/> and <see cref="PitchMotion.Rest"/>.
/// </summary>
/// <param name="NoteId">The <see cref="NoteEvent.Id"/> this gesture describes the arrival of.</param>
/// <param name="Motion">The relative-motion classification (Step 047's own output).</param>
/// <param name="IntervalSemitones">Signed semitone distance from the previous sounding note; <c>0</c> for <see cref="PitchMotion.Repeat"/>/<see cref="PitchMotion.Hold"/>/<see cref="PitchMotion.Rest"/>.</param>
/// <param name="StartTicks">First tick (inclusive) this gesture applies from, half-open.</param>
/// <param name="EndTicks">One past the last tick this gesture applies to; always <c>&gt; StartTicks</c>.</param>
/// <param name="Confidence">Confidence in <c>[0, 1]</c> that this classification is correct -- independent of (and never greater than derivable from) the source <see cref="NoteEvent.Confidence"/>.</param>
public sealed record MelodicGesture(
    Guid NoteId,
    PitchMotion Motion,
    int IntervalSemitones,
    long StartTicks,
    long EndTicks,
    double Confidence);
