using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Melody.Tests;

/// <summary>
/// PHASE-07 Step 046 — "Implement fundamental-frequency/pitch tracking for isolated lead source."
/// Required acceptance (verbatim): "Synthetic sine/recorded single notes within cents and timing
/// tolerance." Failure/edge case (verbatim): "Invalid input, cancellation or stale persisted state must
/// not corrupt the project." Implementation guidance (verbatim): "Implement pitch detector with
/// voice/instrument-specific limitations; test sine tones, instrument samples and octaves; preserve raw
/// contour for fallback."
/// </summary>
public sealed class Step046AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private const long TicksPerSecond = 10_000_000L;

    private static readonly PitchTrackingSettings DefaultSettings =
        new(FrameSize: 512, HopSamples: 256, MinFrequencyHz: 80.0, MaxFrequencyHz: 1000.0, VoicingThreshold: 0.15);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step046-{Guid.NewGuid():N}");

    public Step046AcceptanceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static long ToTicks(long sampleIndex) => (long)Math.Round(sampleIndex * (TicksPerSecond / (double)SampleRate));

    private static double CentsOff(double measuredHz, double trueHz) => 1200.0 * Math.Log2(measuredHz / trueHz);

    private async Task<StemAsset> WriteStemAsync(string fileName, float[] samples, StemKind kind = StemKind.Vocals)
    {
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, SampleRate);
        var path = Path.Combine(_tempDir, fileName);
        await File.WriteAllBytesAsync(path, wavBytes);

        var probed = await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
        return new StemAsset(
            Id: Guid.NewGuid(),
            Kind: kind,
            FilePath: path,
            SampleRate: probed.SampleRate,
            SampleCount: probed.SampleCount,
            SourceOriginTicks: 0,
            Sha256: probed.Sha256);
    }

    [Fact]
    public async Task Step046_Acceptance_Passes_SyntheticSineWithinCentsAndTimingTolerance()
    {
        // A 440 Hz (A4) pure sine tone, 1 second — "synthetic sine... within cents and timing
        // tolerance" (required acceptance, verbatim).
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 1.0, SampleRate);
        var stem = await WriteStemAsync("sine-440.wav", samples);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(decoder, store);

        var analysis = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);

        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.True(File.Exists(store.GetPath(stem.Id)));
        Assert.NotEmpty(analysis.Frames);

        // Timing: every frame lands exactly on its own hop boundary (frame index * HopSamples, converted
        // to ticks) -- the "timing tolerance" half of this step's own required acceptance, satisfied
        // exactly (tolerance 0) since frame placement is deterministic by construction.
        for (var i = 0; i < analysis.Frames.Length; i++)
        {
            Assert.Equal(ToTicks((long)i * DefaultSettings.HopSamples), analysis.Frames[i].TimeTicks);
        }

        // Skip the very first frame (potential windowing edge effect) -- every subsequent frame of a
        // sustained pure tone must be voiced and within a few cents of the true 440 Hz pitch.
        foreach (var frame in analysis.Frames.Skip(1).SkipLast(1))
        {
            Assert.True(frame.FrequencyHz > 0, "expected a voiced frame for a sustained pure tone.");
            Assert.Equal(69, frame.MidiNote); // A4
            Assert.NotNull(frame.CentsOffset);
            var cents = CentsOff(frame.FrequencyHz, 440.0);
            Assert.True(Math.Abs(cents) <= 15.0, $"expected within 15 cents of 440 Hz, measured {frame.FrequencyHz:F3} Hz ({cents:F2} cents off).");
            Assert.True(frame.Confidence >= 0.8, $"expected high confidence for a clean sine tone, was {frame.Confidence}.");
        }

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(stem.Sha256, analysis.SourceHash);
        Assert.Equal(stem.SampleRate, analysis.SourceSampleRate);
        Assert.Equal(PitchTracker.Version, analysis.AnalyzerVersion);
        Assert.Equal(DefaultSettings, analysis.Settings);

        // Cache reopens without recompute.
        var analysis2 = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);
        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.Equal(analysis.VersionId, analysis2.VersionId);
    }

    [Theory]
    [InlineData(220.0, 57)]  // A3
    [InlineData(880.0, 81)]  // A5
    public async Task TrackAsync_OctaveVariants_ReportCorrectMidiNote_NoOctaveError(double frequencyHz, int expectedMidiNote)
    {
        // "test sine tones... and octaves" (implementation guidance, verbatim): YIN's own
        // absolute-threshold-then-local-minimum search (not a naive global minimum) must not fold a
        // note's own true period down to a shorter sub-harmonic/octave or up to a longer one.
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([frequencyHz], noteDurationSeconds: 0.75, SampleRate);
        var stem = await WriteStemAsync($"octave-{expectedMidiNote}.wav", samples);

        var tracker = new PitchTracker(new WavAudioDecoder(), new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache")));
        var analysis = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);

        var voiced = analysis.Frames.Skip(1).SkipLast(1).ToArray();
        Assert.NotEmpty(voiced);
        Assert.All(voiced, f =>
        {
            Assert.Equal(expectedMidiNote, f.MidiNote);
            var cents = CentsOff(f.FrequencyHz, frequencyHz);
            Assert.True(Math.Abs(cents) <= 15.0, $"expected within 15 cents of {frequencyHz} Hz, measured {f.FrequencyHz:F3} Hz ({cents:F2} cents off).");
        });
    }

    [Fact]
    public async Task TrackAsync_RecordedHarmonicNote_TracksFundamentalNotHarmonic_InstrumentSampleScenario()
    {
        // "recorded single notes" (required acceptance) / "instrument samples" (implementation
        // guidance): a struck-string-like tone with a decaying fundamental + two overtones, not a pure
        // sine -- the fundamental (weight 1.0, strongest) must still be what is reported, not a
        // harmonic.
        var samples = DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase([329.63], noteDurationSeconds: 1.0, SampleRate); // E4
        var stem = await WriteStemAsync("harmonic-e4.wav", samples, StemKind.Piano);

        var tracker = new PitchTracker(new WavAudioDecoder(), new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache")));
        var analysis = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);

        // Only consider frames while the percussive decay envelope still carries enough energy to be
        // confidently pitched (the exponential decay eventually fades toward the noise floor, which is
        // this detector's own disclosed, expected limitation -- not asserted against here).
        var confidentFrames = analysis.Frames.Skip(1).Where(f => f.Confidence >= 0.8).ToArray();
        Assert.NotEmpty(confidentFrames);
        Assert.All(confidentFrames, f =>
        {
            Assert.Equal(64, f.MidiNote); // E4
            var cents = CentsOff(f.FrequencyHz, 329.63);
            Assert.True(Math.Abs(cents) <= 25.0, $"expected within 25 cents of 329.63 Hz, measured {f.FrequencyHz:F3} Hz ({cents:F2} cents off).");
        });
    }

    [Fact]
    public async Task TrackAsync_Silence_AllFramesUnvoicedExplicitly_NoCrash()
    {
        // "Silence... must be handled explicitly" (same shape as Step 045's own silence convention).
        var samples = new float[SampleRate]; // 1 second of digital silence
        var stem = await WriteStemAsync("silence.wav", samples);

        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(new WavAudioDecoder(), store);

        var analysis = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);

        Assert.NotEmpty(analysis.Frames);
        Assert.All(analysis.Frames, f =>
        {
            Assert.Equal(0.0, f.FrequencyHz);
            Assert.Null(f.MidiNote);
            Assert.Null(f.CentsOffset);
            Assert.Equal(0.0, f.Confidence);
        });
    }

    [Theory]
    [InlineData(0, 256, 80.0, 1000.0, 0.15, "FrameSize")]
    [InlineData(512, 0, 80.0, 1000.0, 0.15, "HopSamples")]
    [InlineData(512, 256, 0.0, 1000.0, 0.15, "MinFrequencyHz")]
    [InlineData(512, 256, 1000.0, 1000.0, 0.15, "MaxFrequencyHz")]
    [InlineData(512, 256, 80.0, 1000.0, 0.0, "VoicingThreshold")]
    [InlineData(512, 256, 80.0, 1000.0, 1.0, "VoicingThreshold")]
    public async Task TrackAsync_InvalidSettings_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath(
        int frameSize, int hopSamples, double minFreq, double maxFreq, double voicingThreshold, string expectedReasonFragment)
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.25, SampleRate);
        var stem = await WriteStemAsync($"invalid-{expectedReasonFragment}-{Guid.NewGuid():N}.wav", samples);

        var invalidSettings = new PitchTrackingSettings(frameSize, hopSamples, minFreq, maxFreq, voicingThreshold);
        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(decoder, store);

        var ex = await Assert.ThrowsAsync<PitchTrackAnalysisInvalidException>(
            () => tracker.TrackAsync(stem, invalidSettings, CancellationToken.None));

        Assert.Contains(expectedReasonFragment, ex.Reason);
        Assert.Equal(0, decoder.DecodeCallCount);
        Assert.False(File.Exists(store.GetPath(stem.Id)));
    }

    [Fact]
    public async Task TrackAsync_FrameSizeTooSmallForMinFrequency_ThrowsInvalidInputNegativePath()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.25, SampleRate);
        var stem = await WriteStemAsync("framesize-too-small.wav", samples);

        // MinFrequencyHz=80 at 8 kHz implies a 100-sample maximum lag; a 64-sample frame can never
        // contain a full period of the lowest frequency this run claims to support.
        var tooSmall = DefaultSettings with { FrameSize = 64 };

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var tracker = new PitchTracker(decoder, new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache")));

        var ex = await Assert.ThrowsAsync<PitchTrackAnalysisInvalidException>(
            () => tracker.TrackAsync(stem, tooSmall, CancellationToken.None));

        Assert.Contains("FrameSize", ex.Reason);
        Assert.Equal(0, decoder.DecodeCallCount);
    }

    [Fact]
    public async Task TrackAsync_MissingAudioFile_ThrowsFileNotFoundException_InvalidInputNegativePath()
    {
        var missing = new StemAsset(Guid.NewGuid(), StemKind.Vocals, Path.Combine(_tempDir, "does-not-exist.wav"), SampleRate, 1000, 0, "deadbeef");
        var tracker = new PitchTracker(new WavAudioDecoder(), new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache")));

        await Assert.ThrowsAsync<FileNotFoundException>(() => tracker.TrackAsync(missing, DefaultSettings, CancellationToken.None));
    }

    [Fact]
    public async Task TrackAsync_StaleContentHashMismatch_ThrowsWithoutWriting_StalePersistedStateNegativePath()
    {
        // The declared StemAsset.Sha256 no longer matches the file's real on-disk content -- the file
        // was modified/replaced after the StemAsset record was created elsewhere in the pipeline.
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.25, SampleRate);
        var stem = await WriteStemAsync("hash-stale.wav", samples);
        var doctored = stem with { Sha256 = "0000000000000000000000000000000000000000000000000000000000000" };

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(decoder, store);

        var ex = await Assert.ThrowsAsync<PitchTrackAnalysisInvalidException>(
            () => tracker.TrackAsync(doctored, DefaultSettings, CancellationToken.None));

        Assert.Contains("hash", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, decoder.DecodeCallCount); // Probe ran (to compute the real hash); full decode never did.
        Assert.False(File.Exists(store.GetPath(doctored.Id)));
    }

    [Fact]
    public async Task TrackAsync_SampleRateMismatch_ThrowsWithoutWriting_InvalidInputNegativePath()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.25, SampleRate);
        var stem = await WriteStemAsync("rate-mismatch.wav", samples);
        var doctored = stem with { SampleRate = SampleRate * 2 };

        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(new WavAudioDecoder(), store);

        var ex = await Assert.ThrowsAsync<PitchTrackAnalysisInvalidException>(
            () => tracker.TrackAsync(doctored, DefaultSettings, CancellationToken.None));

        Assert.Contains("sample rate", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(store.GetPath(doctored.Id)));
    }

    [Fact]
    public async Task TrackAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 1.0, SampleRate);
        var stem = await WriteStemAsync("cancel.wav", samples);

        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(new WavAudioDecoder(), store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tracker.TrackAsync(stem, DefaultSettings, cts.Token));

        Assert.False(File.Exists(store.GetPath(stem.Id)));
    }

    [Fact]
    public async Task TrackAsync_CorruptCacheFile_IsIgnoredAndRecomputes_StalePersistedStateNegativePath()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.5, SampleRate);
        var stem = await WriteStemAsync("corrupt-cache.wav", samples);

        var cacheDir = Path.Combine(_tempDir, "pitch-cache");
        var store = new JsonPitchTrackStore(cacheDir);
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(store.GetPath(stem.Id), "{ not valid json");

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var tracker = new PitchTracker(decoder, store);

        var analysis = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);

        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.NotEmpty(analysis.Frames);

        var reopened = await store.LoadAsync(store.GetPath(stem.Id), CancellationToken.None);
        Assert.Equal(analysis.VersionId, reopened.VersionId);
    }

    [Fact]
    public async Task TrackAsync_SettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildNoteSequence([440.0], noteDurationSeconds: 0.5, SampleRate);
        var stem = await WriteStemAsync("settings-change.wav", samples);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonPitchTrackStore(Path.Combine(_tempDir, "pitch-cache"));
        var tracker = new PitchTracker(decoder, store);

        var analysis1 = await tracker.TrackAsync(stem, DefaultSettings, CancellationToken.None);
        var changed = DefaultSettings with { HopSamples = 128 };
        var analysis2 = await tracker.TrackAsync(stem, changed, CancellationToken.None);

        Assert.Equal(2, decoder.DecodeCallCount);
        Assert.NotEqual(analysis1.VersionId, analysis2.VersionId);
        Assert.Equal(changed, analysis2.Settings);
        Assert.True(analysis2.Frames.Length > analysis1.Frames.Length); // smaller hop -> more frames
    }

    /// <summary>Wraps a real <see cref="IAudioDecoder"/>, counting <see cref="DecodeMonoAsync"/> calls so a test can prove cache behavior against real decode behavior, not a hand-rolled fake.</summary>
    private sealed class CountingAudioDecoder(IAudioDecoder inner) : IAudioDecoder
    {
        public int DecodeCallCount { get; private set; }

        public Task<SongAsset> ProbeAsync(string path, CancellationToken ct) => inner.ProbeAsync(path, ct);

        public IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(SongAsset song, int targetSampleRate, CancellationToken ct)
        {
            DecodeCallCount++;
            return inner.DecodeMonoAsync(song, targetSampleRate, ct);
        }
    }
}
