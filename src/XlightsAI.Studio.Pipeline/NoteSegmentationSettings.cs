namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Tunable thresholds for <see cref="PitchFrameNoteSegmenter"/> — the same "tunable thresholds, not a
/// hard-coded constant" shape as <c>XlightsAI.Studio.Melody.GestureClassificationSettings</c>.
/// </summary>
/// <param name="MaxPitchJumpSemitones">The largest absolute MIDI-note difference between consecutive voiced frames still treated as the same note; a larger jump ends the current note and starts a new one.</param>
/// <param name="MinFrameConfidence">A frame below this <c>PitchFrame.Confidence</c> is treated as unvoiced for segmentation purposes, ending any in-progress note.</param>
/// <param name="MinNoteDurationTicks">A segmented note shorter than this (canonical 100 ns ticks) is discarded as a spurious blip rather than emitted as a real <c>NoteEvent</c>.</param>
public sealed record NoteSegmentationSettings(
    int MaxPitchJumpSemitones,
    double MinFrameConfidence,
    long MinNoteDurationTicks)
{
    /// <summary>A musically reasonable default: a semitone of pitch wobble within one note is tolerated, frames below 50% voicing confidence end a note, and notes shorter than 50 ms (a single analysis hop or two at typical hop sizes) are treated as noise.</summary>
    public static NoteSegmentationSettings Default { get; } = new(
        MaxPitchJumpSemitones: 1,
        MinFrameConfidence: 0.5,
        MinNoteDurationTicks: 500_000L);
}
