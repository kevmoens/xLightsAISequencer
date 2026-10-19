namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 048's own implementation guidance, verbatim: "Parse MIDI note-on/note-off and tempo
/// changes; create adapter to same track; use MIDI as ground-truth alternative where present." A
/// Standard MIDI File commonly interleaves several channels (melody, bass, drums, ...) across one or more
/// tracks; this step's own scope is a single monophonic melody line (Section 3: polyphony is Step 051's
/// own later scope), so the importer must be told -- or must deterministically infer -- which one MIDI
/// channel is "the melody."
/// </summary>
/// <param name="MelodyChannel">
/// The 0-based MIDI channel (<c>0-15</c>) to import as the melody line, or <c>null</c> to auto-select the
/// channel with the most note-on events, excluding channel 9 (the General MIDI percussion channel, which
/// is never melodic pitch content).
/// </param>
/// <param name="MinNoteConfidence">
/// Confidence assigned to every imported <see cref="NoteEvent"/> -- always <c>1.0</c> by default, since
/// Section 3's own architectural design note treats trusted MIDI as ground truth, not an estimate. Exposed
/// (rather than hard-coded) only so a caller blending MIDI with a lower-trust source can deliberately mark
/// an imported note as less than fully certain; never produced below this value by the importer itself.
/// </param>
public sealed record MidiImportSettings(int? MelodyChannel, double MinNoteConfidence)
{
    public static MidiImportSettings Default { get; } = new(MelodyChannel: null, MinNoteConfidence: 1.0);
}
