namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 045's own implementation guidance, verbatim: "Define note/timing provenance, integer
/// MIDI pitch, velocity and confidence with explicit units; normalize overlaps and invalid duration."
/// Pure, synchronous, no file I/O, no ambient clock -- the same precedent
/// <c>XlightsAI.Studio.Stems.ModelFeasibilityEvaluator</c> established for a phase-local decision gate.
///
/// Two distinct behaviors, named explicitly so a caller never confuses them: a genuinely invalid note
/// (zero/negative duration, out-of-range unit, missing media, a dangling gesture reference) always
/// throws <see cref="MelodyTrackInvalidException"/> -- never silently repaired. A merely *overlapping*
/// pair of otherwise-valid notes is normalized (the earlier note's <see cref="NoteEvent.EndTicks"/> is
/// trimmed to the later note's <see cref="NoteEvent.StartTicks"/>) rather than rejected, because two
/// analyzer-produced note hypotheses overlapping in time is an expected, recoverable condition for a
/// monophonic melody line, not a corrupt input.
/// </summary>
public static class MelodyTrackValidator
{
    private const int MinMidiNote = 0;
    private const int MaxMidiNote = 127;

    /// <summary>
    /// Validates every unit/provenance rule and returns a track whose <see cref="MelodyTrack.Notes"/> are
    /// sorted by <see cref="NoteEvent.StartTicks"/> and have had any overlap trimmed away. Throws
    /// <see cref="MelodyTrackInvalidException"/> for every failure/edge case this step names explicitly
    /// (invalid timing boundaries, out-of-range units, missing media, a dangling gesture reference) --
    /// never partially mutates the input.
    /// </summary>
    public static MelodyTrack Validate(MelodyTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (string.IsNullOrWhiteSpace(track.SourceAssetHash))
        {
            throw new MelodyTrackInvalidException(
                $"MelodyTrack {track.Id} has no SourceAssetHash -- missing media.");
        }

        foreach (var note in track.Notes)
        {
            ValidateNoteUnits(note);
        }

        var normalizedNotes = NormalizeOverlaps(track.Notes);

        var noteIds = new HashSet<Guid>(normalizedNotes.Select(n => n.Id));
        foreach (var gesture in track.Gestures)
        {
            if (!noteIds.Contains(gesture.NoteId))
            {
                throw new MelodyTrackInvalidException(
                    $"MelodicGesture references NoteId {gesture.NoteId}, which is not present in MelodyTrack {track.Id}'s own Notes.");
            }

            if (gesture.EndTicks <= gesture.StartTicks)
            {
                throw new MelodyTrackInvalidException(
                    $"MelodicGesture for NoteId {gesture.NoteId} has invalid timing boundaries: EndTicks ({gesture.EndTicks}) must be greater than StartTicks ({gesture.StartTicks}).");
            }

            if (gesture.Confidence is < 0.0 or > 1.0)
            {
                throw new MelodyTrackInvalidException(
                    $"MelodicGesture for NoteId {gesture.NoteId} has Confidence ({gesture.Confidence}) outside [0, 1].");
            }
        }

        return track with { Notes = normalizedNotes };
    }

    private static void ValidateNoteUnits(NoteEvent note)
    {
        if (note.EndTicks <= note.StartTicks)
        {
            throw new MelodyTrackInvalidException(
                $"NoteEvent {note.Id} has invalid timing boundaries: EndTicks ({note.EndTicks}) must be greater than StartTicks ({note.StartTicks}).");
        }

        if (note.MidiNote is < MinMidiNote or > MaxMidiNote)
        {
            throw new MelodyTrackInvalidException(
                $"NoteEvent {note.Id} has MidiNote ({note.MidiNote}) outside [{MinMidiNote}, {MaxMidiNote}].");
        }

        if (note.Velocity is < 0.0 or > 1.0)
        {
            throw new MelodyTrackInvalidException(
                $"NoteEvent {note.Id} has Velocity ({note.Velocity}) outside [0, 1].");
        }

        if (note.Confidence is < 0.0 or > 1.0)
        {
            throw new MelodyTrackInvalidException(
                $"NoteEvent {note.Id} has Confidence ({note.Confidence}) outside [0, 1].");
        }
    }

    private static IReadOnlyList<NoteEvent> NormalizeOverlaps(IReadOnlyList<NoteEvent> notes)
    {
        if (notes.Count == 0)
        {
            return notes;
        }

        var sorted = notes.OrderBy(n => n.StartTicks).ToList();
        var result = new List<NoteEvent>(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];
            if (i + 1 < sorted.Count)
            {
                var next = sorted[i + 1];
                if (next.StartTicks < current.EndTicks)
                {
                    if (next.StartTicks <= current.StartTicks)
                    {
                        throw new MelodyTrackInvalidException(
                            $"NoteEvent {current.Id} ({current.StartTicks}-{current.EndTicks}) is fully contained by/coincides with NoteEvent {next.Id} ({next.StartTicks}-{next.EndTicks}) -- cannot normalize without producing a zero-length note.");
                    }

                    current = current with { EndTicks = next.StartTicks };
                }
            }

            result.Add(current);
        }

        return result;
    }
}
