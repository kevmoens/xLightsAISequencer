using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 045 — "Define pitch/note/onset and relative melodic gesture contracts, with
/// source/confidence." Required acceptance (verbatim): "Fixture JSON round-trips with units and source
/// IDs." Failure/edge case (verbatim): "Silence, invalid timing boundaries and missing media must be
/// handled explicitly."
/// </summary>
public sealed class Step045AcceptanceTests : IDisposable
{
    private const long TicksPerSecond = 10_000_000L;
    private readonly string _rootDirectory;

    public Step045AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step045-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static NoteEvent Note(long startTicks, long endTicks, int midiNote, double velocity = 0.8, double confidence = 0.95, bool userCorrected = false) =>
        new(Guid.NewGuid(), startTicks, endTicks, midiNote, velocity, confidence, userCorrected);

    [Fact]
    public async Task Step045_Acceptance_Passes_FixtureJsonRoundTripsWithUnitsAndSourceIds()
    {
        // C4-D4-E4-G4-E4-D4-C4 at real (non-evenly-subdivided) onsets -- Section 12's own worked
        // example -- plus one derived gesture per note, deliberately not evenly spaced in time.
        var notes = new[]
        {
            Note(0L, 4_000_000L, 60),              // C4
            Note(4_200_000L, 7_900_000L, 62),      // D4
            Note(8_000_000L, 11_500_000L, 64),     // E4
            Note(11_600_000L, 16_000_000L, 67),    // G4
            Note(16_100_000L, 19_000_000L, 64),    // E4
            Note(19_100_000L, 22_000_000L, 62),    // D4
            Note(22_100_000L, 26_000_000L, 60),    // C4
        };

        var gestures = new List<MelodicGesture>
        {
            new(notes[0].Id, PitchMotion.Rest, 0, notes[0].StartTicks, notes[0].EndTicks, 1.0),
            new(notes[1].Id, PitchMotion.StepUp, 2, notes[1].StartTicks, notes[1].EndTicks, 0.9),
            new(notes[2].Id, PitchMotion.StepUp, 2, notes[2].StartTicks, notes[2].EndTicks, 0.9),
            new(notes[3].Id, PitchMotion.LeapUp, 3, notes[3].StartTicks, notes[3].EndTicks, 0.9),
            new(notes[4].Id, PitchMotion.LeapDown, -3, notes[4].StartTicks, notes[4].EndTicks, 0.9),
            new(notes[5].Id, PitchMotion.StepDown, -2, notes[5].StartTicks, notes[5].EndTicks, 0.9),
            new(notes[6].Id, PitchMotion.StepDown, -2, notes[6].StartTicks, notes[6].EndTicks, 0.9),
        };

        const string sourceAssetHash = "c3b9f1a5e7d2049b8a6f1c4d7e9b0a2f5c8d1e4b7a0f3c6d9e2b5a8f1c4d7e9b";
        var track = new MelodyTrack(Guid.NewGuid(), sourceAssetHash, notes, gestures);

        var store = new JsonMelodyTrackStore(Path.Combine(_rootDirectory, "tracks"));
        var path = await store.SaveAsync(track, CancellationToken.None);
        var reloaded = await store.LoadAsync(path, CancellationToken.None);

        Assert.Equal(track.Id, reloaded.Id);
        Assert.Equal(sourceAssetHash, reloaded.SourceAssetHash); // source IDs round-trip
        Assert.Equal(notes.Length, reloaded.Notes.Count);

        for (var i = 0; i < notes.Length; i++)
        {
            Assert.Equal(notes[i].Id, reloaded.Notes[i].Id);
            Assert.Equal(notes[i].StartTicks, reloaded.Notes[i].StartTicks); // tick units round-trip exactly
            Assert.Equal(notes[i].EndTicks, reloaded.Notes[i].EndTicks);
            Assert.Equal(notes[i].MidiNote, reloaded.Notes[i].MidiNote);    // MIDI note number unit round-trips
            Assert.Equal(notes[i].Velocity, reloaded.Notes[i].Velocity);
            Assert.Equal(notes[i].Confidence, reloaded.Notes[i].Confidence);
        }

        Assert.Equal(gestures.Count, reloaded.Gestures.Count);
        for (var i = 0; i < gestures.Count; i++)
        {
            Assert.Equal(gestures[i].NoteId, reloaded.Gestures[i].NoteId); // gesture-to-note source ID round-trips
            Assert.Equal(gestures[i].Motion, reloaded.Gestures[i].Motion);
            Assert.Equal(gestures[i].IntervalSemitones, reloaded.Gestures[i].IntervalSemitones);
        }
    }

    [Fact]
    public void SilentTrack_EmptyNotesAndGestures_ValidatesWithoutError()
    {
        // Section 3's own "Represent rests explicitly" -- a fully silent source (no detected notes at
        // all) must validate and persist cleanly, not be treated as an error.
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [], []);

        var validated = MelodyTrackValidator.Validate(track);

        Assert.Empty(validated.Notes);
        Assert.Empty(validated.Gestures);
    }

    [Fact]
    public async Task SilentTrack_RoundTripsThroughStore()
    {
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [], []);
        var store = new JsonMelodyTrackStore(Path.Combine(_rootDirectory, "tracks"));

        var path = await store.SaveAsync(track, CancellationToken.None);
        var reloaded = await store.LoadAsync(path, CancellationToken.None);

        Assert.Empty(reloaded.Notes);
        Assert.Empty(reloaded.Gestures);
    }

    [Fact]
    public void InvalidTimingBoundary_EndTicksNotAfterStartTicks_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var badNote = Note(10_000L, 10_000L, 60); // zero-length: EndTicks == StartTicks
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [badNote], []);

        var ex = Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
        Assert.Contains("invalid timing boundaries", ex.Reason);
    }

    [Fact]
    public void InvalidTimingBoundary_EndTicksBeforeStartTicks_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var badNote = Note(10_000L, 5_000L, 60); // reversed
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [badNote], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Fact]
    public void MissingMedia_EmptySourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var track = new MelodyTrack(Guid.NewGuid(), string.Empty, [Note(0, 1000, 60)], []);

        var ex = Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
        Assert.Contains("missing media", ex.Reason);
    }

    [Fact]
    public void MissingMedia_WhitespaceSourceAssetHash_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var track = new MelodyTrack(Guid.NewGuid(), "   ", [Note(0, 1000, 60)], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void OutOfRangeMidiNote_ThrowsMelodyTrackInvalidException_NegativePath(int midiNote)
    {
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [Note(0, 1000, midiNote)], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void OutOfRangeVelocity_ThrowsMelodyTrackInvalidException_NegativePath(double velocity)
    {
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [Note(0, 1000, 60, velocity: velocity)], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void OutOfRangeConfidence_ThrowsMelodyTrackInvalidException_NegativePath(double confidence)
    {
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [Note(0, 1000, 60, confidence: confidence)], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Fact]
    public void DanglingGestureNoteId_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        var note = Note(0, 1000, 60);
        var danglingGesture = new MelodicGesture(Guid.NewGuid(), PitchMotion.StepUp, 2, 0, 1000, 0.9);
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [note], [danglingGesture]);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Fact]
    public void OverlappingNotes_AreNormalizedByTrimmingEarlierNoteEnd_NotRejected()
    {
        // Two analyzer-produced note hypotheses overlap by 500 ticks -- an expected, recoverable
        // condition (Step 045's own implementation guidance: "normalize overlaps"), distinct from a
        // genuinely invalid duration, which must throw instead (see the negative-path tests above).
        var first = Note(0, 5_000, 60);
        var second = Note(4_500, 9_000, 62);
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [second, first], []); // deliberately out of order

        var validated = MelodyTrackValidator.Validate(track);

        Assert.Equal(2, validated.Notes.Count);
        Assert.Equal(first.Id, validated.Notes[0].Id); // re-sorted by StartTicks
        Assert.Equal(0L, validated.Notes[0].StartTicks);
        Assert.Equal(4_500L, validated.Notes[0].EndTicks); // trimmed to the next note's start
        Assert.Equal(second.Id, validated.Notes[1].Id);
        Assert.Equal(4_500L, validated.Notes[1].StartTicks); // untouched
        Assert.Equal(9_000L, validated.Notes[1].EndTicks);
    }

    [Fact]
    public void FullyContainedOverlap_CannotBeNormalized_ThrowsMelodyTrackInvalidException_NegativePath()
    {
        // The second note starts at or before the first note's own start -- trimming would produce a
        // zero-length (or negative-length) note, which is not a recoverable overlap.
        var outer = Note(0, 10_000, 60);
        var innerAtSameStart = Note(0, 2_000, 62);
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [outer, innerAtSameStart], []);

        Assert.Throws<MelodyTrackInvalidException>(() => MelodyTrackValidator.Validate(track));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException_NegativePath()
    {
        var store = new JsonMelodyTrackStore(Path.Combine(_rootDirectory, "tracks"));
        var missingPath = Path.Combine(_rootDirectory, "tracks", "does-not-exist.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.LoadAsync(missingPath, CancellationToken.None));
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ThrowsMelodyTrackCorruptException_NegativePath()
    {
        var directory = Path.Combine(_rootDirectory, "tracks");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "corrupt.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        var store = new JsonMelodyTrackStore(directory);

        await Assert.ThrowsAsync<MelodyTrackCorruptException>(() => store.LoadAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_InvalidTrack_ThrowsBeforeAnyFileIsWritten_NegativePath()
    {
        var directory = Path.Combine(_rootDirectory, "tracks");
        var badNote = Note(10_000, 5_000, 60); // reversed timing
        var track = new MelodyTrack(Guid.NewGuid(), "deadbeef", [badNote], []);
        var store = new JsonMelodyTrackStore(directory);

        await Assert.ThrowsAsync<MelodyTrackInvalidException>(() => store.SaveAsync(track, CancellationToken.None));

        Assert.False(Directory.Exists(directory) && Directory.GetFiles(directory).Length > 0);
    }

    [Fact]
    public void NoteEvent_DurationTicks_MatchesRealSourceTime_NotEvenSubdivision()
    {
        // Section 3's own architectural design note: "Note start/end must follow actual source time,
        // not even subdivisions." Assert the fixture's own deliberately uneven note durations survive
        // unchanged (no rounding to a beat grid anywhere in this contract).
        var note = Note(4_200_000L, 7_900_000L, 62);
        var durationTicks = note.EndTicks - note.StartTicks;
        var durationSeconds = durationTicks / (double)TicksPerSecond;

        Assert.Equal(3_700_000L, durationTicks);
        Assert.Equal(0.37, durationSeconds, precision: 6);
    }
}
