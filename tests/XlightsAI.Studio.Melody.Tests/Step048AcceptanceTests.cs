using static XlightsAI.Studio.Melody.Tests.MidiFixtureBuilder;

namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 048 — "Support MIDI import and evaluate transcription backend behind an interface."
/// Required acceptance (verbatim): "Imported MIDI note timings map to canonical event structure."
/// Implementation guidance (verbatim): "Parse MIDI note-on/note-off and tempo changes; create adapter to
/// same track; use MIDI as ground-truth alternative where present." Failure/edge case (verbatim): "Invalid
/// input, cancellation or stale persisted state must not corrupt the project."
/// </summary>
public sealed class Step048AcceptanceTests : IDisposable
{
    private const int TicksPerQuarterNote = 480;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step048-{Guid.NewGuid():N}");
    private readonly IMidiTranscriptionBackend _backend = new StandardMidiFileTranscriptionBackend();

    public Step048AcceptanceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>Section 12's own worked example: C4-D4-E4-G4-E4-D4-C4, quarter notes, back-to-back, with an explicit tempo change mid-phrase (500,000 -> 1,000,000 microseconds/quarter note) to prove the tempo map, not just a fixed-ratio conversion, is actually applied.</summary>
    private static byte[] BuildWorkedExampleMidi()
    {
        int[] midiNotes = [60, 62, 64, 67, 64, 62, 60]; // C4 D4 E4 G4 E4 D4 C4
        var events = new List<Event> { Tempo(0, 500_000), Tempo(1920, 1_000_000) };

        for (var i = 0; i < midiNotes.Length; i++)
        {
            var start = i * 480L;
            events.Add(NoteOn(start, channel: 0, midiNotes[i], velocity: 100));
            events.Add(NoteOff(start + 480, channel: 0, midiNotes[i], velocity: 64));
        }

        return BuildSingleTrackFile(TicksPerQuarterNote, events);
    }

    [Fact]
    public async Task Step048_Acceptance_Passes_ImportedMidiNoteTimingsMapToCanonicalEventStructure()
    {
        var midiBytes = BuildWorkedExampleMidi();

        var track = await _backend.ImportAsync(midiBytes, sourceAssetHash: "deadbeef", MidiImportSettings.Default, CancellationToken.None);

        // "map to canonical event structure" -- a real XlightsAI.Studio.Melody.MelodyTrack of
        // XlightsAI.Studio.Melody.NoteEvent, not some MIDI-specific shape.
        Assert.IsType<MelodyTrack>(track);
        Assert.Equal(7, track.Notes.Count);

        int[] expectedMidiNotes = [60, 62, 64, 67, 64, 62, 60];
        // First 4 quarter notes at 500,000 us/quarter -> 5,000,000 canonical (100 ns) ticks each (0.5 s);
        // tempo changes to 1,000,000 us/quarter exactly at the tick-1920 boundary (start of note index 4)
        // -> 10,000,000 canonical ticks per quarter note (1.0 s) from then on.
        long[] expectedStart = [0, 5_000_000, 10_000_000, 15_000_000, 20_000_000, 30_000_000, 40_000_000];
        long[] expectedEnd = [5_000_000, 10_000_000, 15_000_000, 20_000_000, 30_000_000, 40_000_000, 50_000_000];

        for (var i = 0; i < 7; i++)
        {
            var note = track.Notes[i];
            Assert.Equal(expectedMidiNotes[i], note.MidiNote);
            Assert.Equal(expectedStart[i], note.StartTicks);
            Assert.Equal(expectedEnd[i], note.EndTicks);
            Assert.Equal(100 / 127.0, note.Velocity, precision: 10);
            Assert.Equal(1.0, note.Confidence); // trusted MIDI -- "use MIDI as ground-truth alternative" (implementation guidance, verbatim)
            Assert.False(note.UserCorrected);
        }

        // "create adapter to same track" (implementation guidance, verbatim) -- the imported track is a
        // complete, gesture-classified MelodyTrack, the same canonical shape Step 047's own classifier
        // produces from pitch-tracked audio, reproducing Section 12's own worked up/leap/down shape exactly.
        Assert.Equal(7, track.Gestures.Count);
        PitchMotion[] expectedMotion = [PitchMotion.Rest, PitchMotion.StepUp, PitchMotion.StepUp, PitchMotion.LeapUp, PitchMotion.LeapDown, PitchMotion.StepDown, PitchMotion.StepDown];
        for (var i = 0; i < 7; i++)
        {
            Assert.Equal(expectedMotion[i], track.Gestures[i].Motion);
        }

        // Saved, persisted, timestamp-provenanced artifact -- this phase's own shared "user-visible
        // outcome" boilerplate, verbatim: "Saved audio/analysis artifact with timestamp provenance."
        var store = new JsonMelodyTrackStore(Path.Combine(_tempDir, "tracks"));
        var path = await store.SaveAsync(track, CancellationToken.None);
        Assert.True(File.Exists(path));
        var reloaded = await store.LoadAsync(path, CancellationToken.None);
        Assert.Equal(track.Id, reloaded.Id);
        Assert.Equal(track.Notes.Count, reloaded.Notes.Count);
    }

    [Fact]
    public async Task ImportAsync_AutoSelectsChannelWithMostNotes_SkippingPercussionChannel9()
    {
        var events = new List<Event>
        {
            // Channel 9 (percussion): 3 hits -- must never be auto-selected regardless of note count.
            NoteOn(0, channel: 9, note: 36, velocity: 100), NoteOff(100, channel: 9, note: 36),
            NoteOn(100, channel: 9, note: 36, velocity: 100), NoteOff(200, channel: 9, note: 36),
            NoteOn(200, channel: 9, note: 36, velocity: 100), NoteOff(300, channel: 9, note: 36),

            // Channel 2 (bass): 1 note.
            NoteOn(0, channel: 2, note: 40, velocity: 90), NoteOff(480, channel: 2, note: 40),

            // Channel 0 (melody): 2 notes -- the real majority non-percussion channel.
            NoteOn(0, channel: 0, note: 60, velocity: 100), NoteOff(480, channel: 0, note: 60),
            NoteOn(480, channel: 0, note: 62, velocity: 100), NoteOff(960, channel: 0, note: 62),
        };

        var midiBytes = BuildSingleTrackFile(TicksPerQuarterNote, events);
        var track = await _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None);

        Assert.Equal(2, track.Notes.Count);
        Assert.Equal([60, 62], track.Notes.Select(n => n.MidiNote).ToArray());
    }

    [Fact]
    public async Task ImportAsync_ExplicitMelodyChannel_SelectsThatChannelEvenIfNotTheMajority()
    {
        var events = new List<Event>
        {
            NoteOn(0, channel: 0, note: 60, velocity: 100), NoteOff(480, channel: 0, note: 60),
            NoteOn(480, channel: 0, note: 62, velocity: 100), NoteOff(960, channel: 0, note: 62),
            NoteOn(0, channel: 5, note: 72, velocity: 100), NoteOff(480, channel: 5, note: 72),
        };

        var midiBytes = BuildSingleTrackFile(TicksPerQuarterNote, events);
        var settings = MidiImportSettings.Default with { MelodyChannel = 5 };
        var track = await _backend.ImportAsync(midiBytes, "deadbeef", settings, CancellationToken.None);

        Assert.Equal([72], track.Notes.Select(n => n.MidiNote).ToArray());
    }

    [Fact]
    public async Task ImportAsync_InvalidHeaderMagic_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var midiBytes = BuildWorkedExampleMidi();
        midiBytes[0] = (byte)'X'; // corrupt "MThd" -> "XThd"

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("MThd", ex.Reason);
    }

    [Fact]
    public async Task ImportAsync_SmpteTimeDivision_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var midiBytes = BuildWorkedExampleMidi();
        // Division is the two big-endian bytes at offset 12; setting the high bit marks SMPTE framing.
        midiBytes[12] |= 0x80;

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("SMPTE", ex.Reason);
    }

    [Fact]
    public async Task ImportAsync_TruncatedTrackChunk_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var midiBytes = WithDeclaredTrackLengthLongerThanActualData(BuildWorkedExampleMidi());

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("length", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedNoteOn_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var events = new List<Event> { NoteOn(0, channel: 0, note: 60, velocity: 100) }; // never closed
        var midiBytes = BuildSingleTrackFile(TicksPerQuarterNote, events);

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("no matching note-off", ex.Reason);
    }

    [Fact]
    public async Task ImportAsync_UnmatchedNoteOff_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var events = new List<Event>
        {
            NoteOn(0, channel: 0, note: 60, velocity: 100), NoteOff(480, channel: 0, note: 60), // a valid pair, so channel 0 auto-selects
            NoteOff(960, channel: 0, note: 62), // never opened
        };
        var midiBytes = BuildSingleTrackFile(TicksPerQuarterNote, events);

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("no matching prior note-on", ex.Reason);
    }

    [Fact]
    public async Task ImportAsync_NoNoteEventsAtAll_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var midiBytes = BuildSingleTrackFile(TicksPerQuarterNote, [Tempo(0, 500_000)]);

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("no note-on events", ex.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_RequestedChannelHasNoNotes_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var midiBytes = BuildWorkedExampleMidi(); // all notes on channel 0
        var settings = MidiImportSettings.Default with { MelodyChannel = 7 };

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", settings, CancellationToken.None));
        Assert.Contains("channel 7", ex.Reason);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public async Task ImportAsync_MelodyChannelOutOfRange_ThrowsMidiImportInvalidException_InvalidInputNegativePath(int channel)
    {
        var midiBytes = BuildWorkedExampleMidi();
        var settings = MidiImportSettings.Default with { MelodyChannel = channel };

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", settings, CancellationToken.None));
        Assert.Contains("MelodyChannel", ex.Reason);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public async Task ImportAsync_MinNoteConfidenceOutOfRange_ThrowsMidiImportInvalidException_InvalidInputNegativePath(double confidence)
    {
        var midiBytes = BuildWorkedExampleMidi();
        var settings = MidiImportSettings.Default with { MinNoteConfidence = confidence };

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", settings, CancellationToken.None));
        Assert.Contains("MinNoteConfidence", ex.Reason);
    }

    [Fact]
    public async Task ImportAsync_EmptySourceAssetHash_ThrowsMidiImportInvalidException_InvalidInputNegativePath()
    {
        var midiBytes = BuildWorkedExampleMidi();

        var ex = await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(midiBytes, sourceAssetHash: "", MidiImportSettings.Default, CancellationToken.None));
        Assert.Contains("missing media", ex.Reason);
    }

    [Fact]
    public async Task ImportAsync_CancelledBeforeParsing_ThrowsWithoutProducingATrack_CancellationNegativePath()
    {
        var midiBytes = BuildWorkedExampleMidi();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", MidiImportSettings.Default, cts.Token));
    }

    [Fact]
    public async Task ImportAsync_InvalidReimport_DoesNotCorruptPreviouslyPersistedTrack_StalePersistedStateNegativePath()
    {
        // A valid import is saved first -- this step's own real, persisted, timestamp-provenanced artifact.
        var validMidi = BuildWorkedExampleMidi();
        var validTrack = await _backend.ImportAsync(validMidi, "deadbeef", MidiImportSettings.Default, CancellationToken.None);

        var store = new JsonMelodyTrackStore(Path.Combine(_tempDir, "tracks"));
        var savedPath = await store.SaveAsync(validTrack, CancellationToken.None);
        var savedBytesBefore = await File.ReadAllBytesAsync(savedPath);

        // A later, unrelated invalid import must fail loudly and must never touch the file already saved
        // above -- "stale persisted state must not corrupt the project" (failure/edge case, verbatim).
        var corruptMidi = BuildWorkedExampleMidi();
        corruptMidi[0] = (byte)'X';
        await Assert.ThrowsAsync<MidiImportInvalidException>(
            () => _backend.ImportAsync(corruptMidi, "deadbeef", MidiImportSettings.Default, CancellationToken.None));

        var savedBytesAfter = await File.ReadAllBytesAsync(savedPath);
        Assert.Equal(savedBytesBefore, savedBytesAfter);

        var reloaded = await store.LoadAsync(savedPath, CancellationToken.None);
        Assert.Equal(validTrack.Id, reloaded.Id);
        Assert.Equal(validTrack.Notes.Count, reloaded.Notes.Count);
    }

    [Fact]
    public async Task ImportAsync_NullMidiData_ThrowsArgumentNullException_NegativePath()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _backend.ImportAsync(null!, "deadbeef", MidiImportSettings.Default, CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_NullSettings_ThrowsArgumentNullException_NegativePath()
    {
        var midiBytes = BuildWorkedExampleMidi();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _backend.ImportAsync(midiBytes, "deadbeef", null!, CancellationToken.None));
    }
}
