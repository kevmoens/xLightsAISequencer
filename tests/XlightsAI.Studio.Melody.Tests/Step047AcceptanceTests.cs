namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 047 — "Derive up/down/repeat/hold/leap/rest from melodic contour with noise filtering."
/// Required acceptance (verbatim): "Synthetic melodic shapes classify correctly." Failure/edge case
/// (verbatim, shared with Steps 045/050/051): "Silence, invalid timing boundaries and missing media must
/// be handled explicitly."
/// </summary>
public sealed class Step047AcceptanceTests
{
    private const string Hash = "deadbeef";

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote, double confidence = 0.9) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, Velocity: 0.8, confidence, UserCorrected: false);

    private static MelodyTrack Track(params NoteEvent[] notes) =>
        new(Guid.NewGuid(), Hash, notes, []);

    [Fact]
    public void Step047_Acceptance_Passes_SyntheticMelodicShapesClassifyCorrectly()
    {
        // Section 12's own worked example, and Step 045's own already-committed fixture
        // (Step045AcceptanceTests.Step045_Acceptance_Passes_...): C4-D4-E4-G4-E4-D4-C4 at real,
        // non-evenly-subdivided onsets with small (< 1 real rest) inter-note phrase gaps.
        var notes = new[]
        {
            Note(0L, 4_000_000L, 60),           // C4
            Note(4_200_000L, 7_900_000L, 62),   // D4
            Note(8_000_000L, 11_500_000L, 64),  // E4
            Note(11_600_000L, 16_000_000L, 67), // G4
            Note(16_100_000L, 19_000_000L, 64), // E4
            Note(19_100_000L, 22_000_000L, 62), // D4
            Note(22_100_000L, 26_000_000L, 60), // C4
        };

        var classified = MelodicContourClassifier.Classify(Track(notes), GestureClassificationSettings.Default);

        Assert.Equal(7, classified.Gestures.Count);

        var expected = new (Guid NoteId, PitchMotion Motion, int Interval)[]
        {
            (notes[0].Id, PitchMotion.Rest, 0),       // arrives out of implied silence
            (notes[1].Id, PitchMotion.StepUp, 2),     // C4 -> D4
            (notes[2].Id, PitchMotion.StepUp, 2),     // D4 -> E4
            (notes[3].Id, PitchMotion.LeapUp, 3),     // E4 -> G4
            (notes[4].Id, PitchMotion.LeapDown, -3),  // G4 -> E4
            (notes[5].Id, PitchMotion.StepDown, -2),  // E4 -> D4
            (notes[6].Id, PitchMotion.StepDown, -2),  // D4 -> C4
        };

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].NoteId, classified.Gestures[i].NoteId);
            Assert.Equal(expected[i].Motion, classified.Gestures[i].Motion);
            Assert.Equal(expected[i].Interval, classified.Gestures[i].IntervalSemitones);
        }
    }

    [Fact]
    public void RepeatedPitch_WithAudibleButSubRestGap_ClassifiesAsRepeat_NotStepOrHold()
    {
        var first = Note(0L, 5_000L, 60);
        var second = Note(5_500L, 10_000L, 60); // same pitch, 500-tick re-attack gap (well under the 1,000,000-tick rest threshold)

        var classified = MelodicContourClassifier.Classify(Track(first, second), GestureClassificationSettings.Default);

        Assert.Equal(PitchMotion.Rest, classified.Gestures[0].Motion); // first note: no predecessor
        Assert.Equal(PitchMotion.Repeat, classified.Gestures[1].Motion);
        Assert.Equal(0, classified.Gestures[1].IntervalSemitones);
    }

    [Fact]
    public void SamePitch_TouchingWithNoGap_ClassifiesAsHold_NotRepeat()
    {
        var first = Note(0L, 5_000L, 60);
        var second = Note(5_000L, 10_000L, 60); // touches exactly -- sustained, not re-attacked

        var classified = MelodicContourClassifier.Classify(Track(first, second), GestureClassificationSettings.Default);

        Assert.Equal(PitchMotion.Hold, classified.Gestures[1].Motion);
        Assert.Equal(0, classified.Gestures[1].IntervalSemitones);
    }

    [Fact]
    public void OverlappingSamePitchNotes_TrimmedToTouchingByValidator_StillClassifiesAsHold()
    {
        // MelodyTrackValidator trims the earlier note's EndTicks to the later note's StartTicks before
        // this step's own motion classification runs -- an overlap-trimmed same-pitch pair must read as
        // "touching" (Hold), not accidentally as a small negative gap producing some other motion.
        var first = Note(0L, 5_000L, 60);
        var second = Note(4_500L, 9_000L, 60); // overlaps by 500 ticks, same pitch

        var classified = MelodicContourClassifier.Classify(Track(second, first), GestureClassificationSettings.Default); // deliberately out of order

        Assert.Equal(4_500L, classified.Notes[0].EndTicks); // confirms the trim actually happened
        Assert.Equal(PitchMotion.Hold, classified.Gestures[1].Motion);
    }

    [Fact]
    public void LargeGapBetweenNotes_ClassifiesArrivingNoteAsRest()
    {
        var first = Note(0L, 5_000L, 60);
        var second = Note(2_000_000L, 2_005_000L, 64); // 1,995,000-tick silent gap -- a real musical rest

        var classified = MelodicContourClassifier.Classify(Track(first, second), GestureClassificationSettings.Default);

        Assert.Equal(PitchMotion.Rest, classified.Gestures[1].Motion);
        Assert.Equal(0, classified.Gestures[1].IntervalSemitones);
    }

    [Fact]
    public void NoiseFiltering_LowConfidenceGraceNote_DoesNotCorruptTheFollowingRealNotesInterval()
    {
        // Section 3: "measure musical interval not only note index." A has an ordinary confidence; B is a
        // low-confidence ornamental blip one semitone above A; C is the next real note, a whole step
        // above A. Index-based differencing (C vs B) would report +1 (StepUp); correct musical-interval
        // differencing against the last *confident* note (C vs A) must report +2 (StepUp, matching the
        // real C4->D4 interval) -- this is this step's own named "noise filtering."
        var a = Note(0L, 1_000L, 60, confidence: 0.9);
        var b = Note(1_100L, 1_200L, 61, confidence: 0.1); // noisy grace note, below MinNoteConfidence (0.5)
        var c = Note(1_300L, 1_400L, 62, confidence: 0.9);

        var classified = MelodicContourClassifier.Classify(Track(a, b, c), GestureClassificationSettings.Default);

        Assert.Equal(PitchMotion.StepUp, classified.Gestures[1].Motion); // B still gets its own gesture...
        Assert.Equal(1, classified.Gestures[1].IntervalSemitones);       // ...computed against A (1 semitone)

        Assert.Equal(PitchMotion.StepUp, classified.Gestures[2].Motion); // C is classified against A, not B
        Assert.Equal(2, classified.Gestures[2].IntervalSemitones);       // the real C4->D4 interval, not 1
    }

    [Fact]
    public void Silence_EmptyNotes_ProducesEmptyGestures_NoCrash_EdgeCase()
    {
        var classified = MelodicContourClassifier.Classify(Track(), GestureClassificationSettings.Default);

        Assert.Empty(classified.Notes);
        Assert.Empty(classified.Gestures);
    }

    [Fact]
    public void InvalidTimingBoundary_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var badNote = Note(10_000L, 5_000L, 60); // reversed timing

        Assert.Throws<MelodyTrackInvalidException>(
            () => MelodicContourClassifier.Classify(Track(badNote), GestureClassificationSettings.Default));
    }

    [Fact]
    public void MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var track = new MelodyTrack(Guid.NewGuid(), string.Empty, [Note(0, 1_000, 60)], []);

        var ex = Assert.Throws<MelodyTrackInvalidException>(
            () => MelodicContourClassifier.Classify(track, GestureClassificationSettings.Default));
        Assert.Contains("missing media", ex.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidSettings_NonPositiveStepThreshold_ThrowsMelodyTrackInvalidException_NegativePath(int stepThreshold)
    {
        var settings = new GestureClassificationSettings(stepThreshold, RestGapTicks: 1_000_000L, MinNoteConfidence: 0.5);

        var ex = Assert.Throws<MelodyTrackInvalidException>(
            () => MelodicContourClassifier.Classify(Track(Note(0, 1_000, 60)), settings));
        Assert.Contains("StepThresholdSemitones", ex.Reason);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void InvalidSettings_NonPositiveRestGapTicks_ThrowsMelodyTrackInvalidException_NegativePath(long restGapTicks)
    {
        var settings = new GestureClassificationSettings(StepThresholdSemitones: 2, restGapTicks, MinNoteConfidence: 0.5);

        var ex = Assert.Throws<MelodyTrackInvalidException>(
            () => MelodicContourClassifier.Classify(Track(Note(0, 1_000, 60)), settings));
        Assert.Contains("RestGapTicks", ex.Reason);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void InvalidSettings_OutOfRangeMinNoteConfidence_ThrowsMelodyTrackInvalidException_NegativePath(double minNoteConfidence)
    {
        var settings = new GestureClassificationSettings(StepThresholdSemitones: 2, RestGapTicks: 1_000_000L, minNoteConfidence);

        var ex = Assert.Throws<MelodyTrackInvalidException>(
            () => MelodicContourClassifier.Classify(Track(Note(0, 1_000, 60)), settings));
        Assert.Contains("MinNoteConfidence", ex.Reason);
    }

    [Fact]
    public void NullTrack_ThrowsArgumentNullException_NegativePath()
    {
        Assert.Throws<ArgumentNullException>(
            () => MelodicContourClassifier.Classify(null!, GestureClassificationSettings.Default));
    }

    [Fact]
    public void NullSettings_ThrowsArgumentNullException_NegativePath()
    {
        Assert.Throws<ArgumentNullException>(
            () => MelodicContourClassifier.Classify(Track(Note(0, 1_000, 60)), null!));
    }
}
