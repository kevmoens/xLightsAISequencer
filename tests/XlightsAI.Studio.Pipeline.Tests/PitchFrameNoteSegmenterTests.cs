using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Pipeline;

namespace XlightsAI.Studio.Pipeline.Tests;

/// <summary>
/// <see cref="PitchFrameNoteSegmenter"/> is new, real DSP/segmentation logic this project introduces
/// (no prior code anywhere converted a continuous <see cref="PitchTrackAnalysis"/> contour into discrete
/// <see cref="NoteEvent"/>s) — these tests exercise it against synthetic frame sequences built by hand,
/// the same "deterministic, real-fixture" convention the rest of this repository's acceptance tests use.
/// </summary>
public sealed class PitchFrameNoteSegmenterTests
{
    private static readonly PitchTrackingSettings Settings = new(
        FrameSize: 1024, HopSamples: 441, MinFrequencyHz: 80, MaxFrequencyHz: 1000, VoicingThreshold: 0.15);

    private const int SampleRate = 44100;
    private static readonly long HopTicks = (long)Math.Round(441.0 / SampleRate * 10_000_000L);

    private static PitchFrame Voiced(int index, int midiNote, double confidence = 0.9) =>
        new(index * HopTicks, FrequencyHz: 440.0, MidiNote: midiNote, CentsOffset: 0.0, Confidence: confidence);

    private static PitchFrame Unvoiced(int index) =>
        new(index * HopTicks, FrequencyHz: 0.0, MidiNote: null, CentsOffset: null, Confidence: 0.0);

    private static PitchTrackAnalysis Analysis(params PitchFrame[] frames) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "deadbeef", SampleRate, "test/1.0.0", Settings, frames);

    [Fact]
    public void Segment_SingleSustainedNote_ProducesOneNoteSpanningAllFrames()
    {
        var frames = Enumerable.Range(0, 20).Select(i => Voiced(i, 60)).ToArray();
        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), NoteSegmentationSettings.Default);

        var note = Assert.Single(notes);
        Assert.Equal(60, note.MidiNote);
        Assert.Equal(0L, note.StartTicks);
        Assert.Equal(frames[^1].TimeTicks + HopTicks, note.EndTicks);
        Assert.True(note.Confidence > 0.8);
    }

    [Fact]
    public void Segment_UnvoicedGapBetweenTwoNotes_ProducesTwoSeparateNotes()
    {
        var frames = Enumerable.Range(0, 10).Select(i => Voiced(i, 60))
            .Concat(Enumerable.Range(10, 5).Select(Unvoiced))
            .Concat(Enumerable.Range(15, 10).Select(i => Voiced(i, 64)))
            .ToArray();

        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), NoteSegmentationSettings.Default);

        Assert.Equal(2, notes.Count);
        Assert.Equal(60, notes[0].MidiNote);
        Assert.Equal(64, notes[1].MidiNote);
        Assert.True(notes[0].EndTicks <= notes[1].StartTicks);
    }

    [Fact]
    public void Segment_PitchJumpBeyondThreshold_EndsNoteAndStartsNewOne_NoGapNeeded()
    {
        // A direct pitch jump (no unvoiced gap) past MaxPitchJumpSemitones must still split into two
        // notes, not one note that silently changed pitch midway.
        var frames = Enumerable.Range(0, 10).Select(i => Voiced(i, 60))
            .Concat(Enumerable.Range(10, 10).Select(i => Voiced(i, 72)))
            .ToArray();

        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), NoteSegmentationSettings.Default);

        Assert.Equal(2, notes.Count);
        Assert.Equal(60, notes[0].MidiNote);
        Assert.Equal(72, notes[1].MidiNote);
    }

    [Fact]
    public void Segment_SmallPitchWobbleWithinTolerance_StaysOneNote()
    {
        // A 1-semitone wobble (default MaxPitchJumpSemitones) must not fragment a single sustained note.
        var frames = new[]
        {
            Voiced(0, 60), Voiced(1, 61), Voiced(2, 60), Voiced(3, 60), Voiced(4, 61),
        };

        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), NoteSegmentationSettings.Default);

        Assert.Single(notes);
    }

    [Fact]
    public void Segment_NoteShorterThanMinDuration_IsDroppedAsBlip()
    {
        var frames = new[] { Unvoiced(0), Voiced(1, 60), Unvoiced(2) };
        var settings = NoteSegmentationSettings.Default with { MinNoteDurationTicks = 10_000_000L };

        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), settings);

        Assert.Empty(notes);
    }

    [Fact]
    public void Segment_LowConfidenceFrame_TreatedAsUnvoiced()
    {
        var frames = new[]
        {
            Voiced(0, 60), Voiced(1, 60, confidence: 0.1), Voiced(2, 60),
        };
        var settings = NoteSegmentationSettings.Default with { MinNoteDurationTicks = 0L };

        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), settings);

        // The low-confidence middle frame ends the first note; the third frame starts a second one.
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void Segment_EmptyFrames_ReturnsNoNotes()
    {
        var notes = PitchFrameNoteSegmenter.Segment(Analysis(), NoteSegmentationSettings.Default);
        Assert.Empty(notes);
    }

    [Fact]
    public void Segment_AllUnvoiced_ReturnsNoNotes()
    {
        var frames = Enumerable.Range(0, 5).Select(Unvoiced).ToArray();
        var notes = PitchFrameNoteSegmenter.Segment(Analysis(frames), NoteSegmentationSettings.Default);
        Assert.Empty(notes);
    }

    [Fact]
    public void Segment_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => PitchFrameNoteSegmenter.Segment(null!, NoteSegmentationSettings.Default));
        Assert.Throws<ArgumentNullException>(() => PitchFrameNoteSegmenter.Segment(Analysis(), null!));
    }
}
