namespace XlightsAI.Studio.Melody;

/// <summary>
/// PHASE-07 Step 047 — "Derive up/down/repeat/hold/leap/rest from melodic contour with noise filtering."
/// Pure, synchronous, no file I/O, no ambient clock -- the same shape as <see cref="MelodyTrackValidator"/>
/// (Step 045's own precedent for a phase-local pure domain transform). Classifies every
/// <see cref="NoteEvent"/> in a <see cref="MelodyTrack"/> into a <see cref="MelodicGesture"/> describing
/// its relative motion from the last *confident* preceding note (Section 3's own architectural design
/// note: "build the gesture mapping from relative motion so an octave error does not necessarily ruin a
/// lighting phrase" -- interval is always musical semitone distance, never list-index distance), then
/// re-runs the resulting track through <see cref="MelodyTrackValidator.Validate"/> so every one of Step
/// 045's own already-proven failure/edge cases ("Silence, invalid timing boundaries and missing media
/// must be handled explicitly" -- this step's own identical, verbatim failure/edge case text) is enforced
/// exactly once, in exactly one place, rather than re-implemented here.
/// </summary>
public static class MelodicContourClassifier
{
    /// <summary>
    /// Replaces <paramref name="track"/>'s own <see cref="MelodyTrack.Gestures"/> with a freshly
    /// classified set derived from its <see cref="MelodyTrack.Notes"/> (any gestures already present on
    /// <paramref name="track"/> are discarded -- deliberate: this step's own job is to (re)compute the
    /// gesture vocabulary from the notes, not to merge with a caller-supplied guess). Throws
    /// <see cref="MelodyTrackInvalidException"/> for invalid <paramref name="settings"/>, invalid note
    /// timing boundaries or missing media (delegated to <see cref="MelodyTrackValidator.Validate"/>) --
    /// never silently repairs or partially classifies.
    /// </summary>
    public static MelodyTrack Classify(MelodyTrack track, GestureClassificationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSettings(settings);

        // Reuse Step 045's own validator for timing/unit/media rules; also normalizes/sorts Notes (e.g.
        // trims note-to-note overlaps to touching) before this step's own motion classification runs, so
        // a trimmed-to-zero-gap same-pitch pair is correctly seen as "touching" below.
        var validated = MelodyTrackValidator.Validate(track with { Gestures = [] });

        var gestures = ClassifyNotes(validated.Notes, settings);

        return MelodyTrackValidator.Validate(validated with { Gestures = gestures });
    }

    private static void ValidateSettings(GestureClassificationSettings settings)
    {
        if (settings.StepThresholdSemitones <= 0)
        {
            throw new MelodyTrackInvalidException(
                $"GestureClassificationSettings.StepThresholdSemitones ({settings.StepThresholdSemitones}) must be positive.");
        }

        if (settings.RestGapTicks <= 0)
        {
            throw new MelodyTrackInvalidException(
                $"GestureClassificationSettings.RestGapTicks ({settings.RestGapTicks}) must be positive.");
        }

        if (settings.MinNoteConfidence is < 0.0 or > 1.0)
        {
            throw new MelodyTrackInvalidException(
                $"GestureClassificationSettings.MinNoteConfidence ({settings.MinNoteConfidence}) must be in [0, 1].");
        }
    }

    private static List<MelodicGesture> ClassifyNotes(IReadOnlyList<NoteEvent> notes, GestureClassificationSettings settings)
    {
        var gestures = new List<MelodicGesture>(notes.Count);

        // The last note still trusted as a motion reference point -- a note below MinNoteConfidence still
        // gets its own gesture (classified against this same reference), but never replaces it; this is
        // this step's own named "noise filtering" (see GestureClassificationSettings.MinNoteConfidence).
        NoteEvent? lastConfident = null;

        foreach (var note in notes)
        {
            if (lastConfident is not { } previous)
            {
                // No confident predecessor yet (either the very first note, or every note so far has been
                // noise) -- arrives out of implied silence. Mirrors Step 045's own already-committed
                // fixture (Step045AcceptanceTests: the first note of the C-D-E-G-E-D-C sequence is Rest).
                gestures.Add(new MelodicGesture(note.Id, PitchMotion.Rest, 0, note.StartTicks, note.EndTicks, note.Confidence));
            }
            else
            {
                gestures.Add(ClassifyAgainst(note, previous, settings));
            }

            if (note.Confidence >= settings.MinNoteConfidence)
            {
                lastConfident = note;
            }
        }

        return gestures;
    }

    private static MelodicGesture ClassifyAgainst(NoteEvent note, NoteEvent previous, GestureClassificationSettings settings)
    {
        var gapTicks = note.StartTicks - previous.EndTicks;

        if (gapTicks >= settings.RestGapTicks)
        {
            return new MelodicGesture(note.Id, PitchMotion.Rest, 0, note.StartTicks, note.EndTicks, note.Confidence);
        }

        var interval = note.MidiNote - previous.MidiNote; // musical interval (Section 3), never index distance
        var confidence = Math.Min(note.Confidence, previous.Confidence);

        var motion = interval switch
        {
            0 when gapTicks <= 0 => PitchMotion.Hold,   // touching/overlap-trimmed same pitch: one sustained note, not a re-attack
            0 => PitchMotion.Repeat,                    // same pitch, re-articulated after an audible (sub-rest) gap
            > 0 when interval <= settings.StepThresholdSemitones => PitchMotion.StepUp,
            < 0 when -interval <= settings.StepThresholdSemitones => PitchMotion.StepDown,
            > 0 => PitchMotion.LeapUp,
            _ => PitchMotion.LeapDown,
        };

        return new MelodicGesture(note.Id, motion, interval, note.StartTicks, note.EndTicks, confidence);
    }
}
