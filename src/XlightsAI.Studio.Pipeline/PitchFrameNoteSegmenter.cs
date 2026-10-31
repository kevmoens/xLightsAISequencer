using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Groups <see cref="PitchTrackAnalysis"/>'s raw per-hop pitch contour into discrete <see cref="NoteEvent"/>s
/// — the real algorithm nothing in the codebase provided before this pipeline (confirmed by a full
/// repository search before writing this): <c>Melody.IPitchTracker</c> only produces a continuous
/// per-frame contour, and <c>Melody.MelodicContourClassifier</c> only consumes already-discrete notes.
/// A frame extends the current note while it stays voiced (<see cref="PitchFrame.MidiNote"/> not
/// <see langword="null"/>, <see cref="PitchFrame.Confidence"/> at or above
/// <see cref="NoteSegmentationSettings.MinFrameConfidence"/>) and within
/// <see cref="NoteSegmentationSettings.MaxPitchJumpSemitones"/> of the note's own starting pitch; an
/// unvoiced frame or a larger pitch jump ends the current note (and starts a new one, for a pitch jump).
/// A segmented note shorter than <see cref="NoteSegmentationSettings.MinNoteDurationTicks"/> is dropped as
/// a spurious blip rather than emitted.
/// </summary>
public static class PitchFrameNoteSegmenter
{
    private const long TicksPerSecond = 10_000_000L;

    public static IReadOnlyList<NoteEvent> Segment(PitchTrackAnalysis analysis, NoteSegmentationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(settings);

        var notes = new List<NoteEvent>();
        long? noteStartTicks = null;
        var noteMidiNote = 0;
        var confidenceSum = 0.0;
        var frameCount = 0;

        void FlushNote(long endTicks)
        {
            if (noteStartTicks is null)
            {
                return;
            }

            if (endTicks - noteStartTicks.Value >= settings.MinNoteDurationTicks)
            {
                var confidence = Math.Clamp(confidenceSum / frameCount, 0.0, 1.0);
                notes.Add(new NoteEvent(
                    Id: Guid.NewGuid(),
                    StartTicks: noteStartTicks.Value,
                    EndTicks: endTicks,
                    MidiNote: noteMidiNote,
                    Velocity: confidence,
                    Confidence: confidence,
                    UserCorrected: false));
            }

            noteStartTicks = null;
            confidenceSum = 0.0;
            frameCount = 0;
        }

        foreach (var frame in analysis.Frames)
        {
            var isVoiced = frame.MidiNote is not null && frame.Confidence >= settings.MinFrameConfidence;
            if (!isVoiced)
            {
                FlushNote(frame.TimeTicks);
                continue;
            }

            var midiNote = frame.MidiNote!.Value;
            if (noteStartTicks is not null && Math.Abs(midiNote - noteMidiNote) > settings.MaxPitchJumpSemitones)
            {
                FlushNote(frame.TimeTicks);
            }

            if (noteStartTicks is null)
            {
                noteStartTicks = frame.TimeTicks;
                noteMidiNote = midiNote;
            }

            confidenceSum += frame.Confidence;
            frameCount++;
        }

        if (noteStartTicks is not null)
        {
            var hopTicks = analysis.SourceSampleRate > 0
                ? (long)Math.Round(analysis.Settings.HopSamples / (double)analysis.SourceSampleRate * TicksPerSecond)
                : 0L;
            var lastFrameTicks = analysis.Frames.Length > 0 ? analysis.Frames[^1].TimeTicks : noteStartTicks.Value;
            FlushNote(lastFrameTicks + hopTicks);
        }

        return notes;
    }
}
