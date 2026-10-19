namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Section 3's own proposed signature, verbatim: one pitched note/onset with explicit units
/// and provenance -- an integer MIDI note number (Section 2 convention for pitch, never a raw
/// Hz/frequency value), a <see cref="Confidence"/> separate from <see cref="Velocity"/> (an analyzer
/// can be certain about loudness while uncertain about pitch, or vice versa), and
/// <see cref="UserCorrected"/> so a human edit is distinguishable from the original transcription
/// forever, even after the two are merged into one <see cref="MelodyTrack"/> (Section 4: "Store
/// original transcription version and corrected MelodyTrack as separate revisions").
/// <see cref="StartTicks"/>/<see cref="EndTicks"/> are canonical monotonic-clock ticks (Section 2),
/// half-open (<c>[Start,End)</c>) -- never evenly-subdivided beat positions (Section 3's own
/// architectural design note: "Note start/end must follow actual source time, not even subdivisions").
/// </summary>
/// <param name="Id">Stable identity for this one note, independent of its pitch/timing -- a correction changes <see cref="MidiNote"/>/<see cref="StartTicks"/>/<see cref="EndTicks"/> without ever changing <see cref="Id"/>, so a later-phase effect-link foreign key (Section 4: "preserve link from effect IDs to source note IDs") never dangles across a correction.</param>
/// <param name="StartTicks">First tick (inclusive) of this note, half-open.</param>
/// <param name="EndTicks">One past the last tick of this note; always <c>&gt; StartTicks</c> for a valid note.</param>
/// <param name="MidiNote">Integer MIDI note number (<c>0-127</c>; 60 = middle C), never a raw frequency.</param>
/// <param name="Velocity">Normalized loudness/onset strength in <c>[0, 1]</c> -- not raw MIDI <c>0-127</c> velocity, to keep this contract usable whether the note originated from real MIDI or from pitch-tracked audio (which has no native 0-127 velocity byte).</param>
/// <param name="Confidence">Analyzer confidence in <c>[0, 1]</c> that this note's pitch/timing is correct -- independent of <see cref="Velocity"/>. Always <c>1.0</c> for a trusted-MIDI or <see cref="UserCorrected"/> note.</param>
/// <param name="UserCorrected">True once a human has edited this note's pitch or timing (Step 049's own piano-roll correction boundary); an uncorrected, analyzer-produced note is always <c>false</c>.</param>
public sealed record NoteEvent(
    Guid Id,
    long StartTicks,
    long EndTicks,
    int MidiNote,
    double Velocity,
    double Confidence,
    bool UserCorrected);
