using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-05 / Step 031 — "Implement RMS/energy envelope with baseline comparison." Required
/// acceptance: "Synthetic envelope matches expected energy peaks." Failure/edge case: "Invalid input,
/// cancellation or stale persisted state must not corrupt the project." Implementation guidance:
/// "Compute RMS and spectral features with unit normalization; compare to reference numbers for
/// synthetic tones and silence; document FFT window/hop" (window = 256 samples, hop = 128 samples at
/// the fixture's own 8 kHz rate throughout this file — 16 ms window, 50% overlap).
/// </summary>
public sealed class Step031AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings Settings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step031-{Guid.NewGuid():N}");

    public Step031AcceptanceTests()
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

    private async Task<SongAsset> ProbeAsync(string fileName, float[] samples)
    {
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, SampleRate);
        var path = Path.Combine(_tempDir, fileName);
        await File.WriteAllBytesAsync(path, wavBytes);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    private async Task<SongAsset> ProbeClickTrackAsync(string fileName, int bpm, int clickCount)
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, SampleRate);
        return await ProbeAsync(fileName, samples);
    }

    [Fact]
    public async Task Step031_Acceptance_Passes_SyntheticEnvelopeMatchesExpectedEnergyPeaks()
    {
        // 120 BPM => one click every 0.5 s = 4000 samples at 8 kHz; each click is a 160-sample
        // (0.02 s) percussive burst followed by silence until the next click.
        var asset = await ProbeClickTrackAsync("click-track.wav", bpm: 120, clickCount: 6);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache"));
        var analyzer = new EnergyEnvelopeAnalyzer(decoder, store);

        var envelope = await analyzer.AnalyzeAsync(asset, Settings, CancellationToken.None);

        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.True(File.Exists(store.GetPath(asset.Id)));

        // Frames contiguously and exactly cover [0, SampleCount) — real dimension verification.
        long expectedStart = 0;
        foreach (var frame in envelope.Frames)
        {
            Assert.Equal(expectedStart, frame.StartSample);
            Assert.True(frame.EndSample > frame.StartSample);
            expectedStart += Settings.HopSamples;
        }

        // "Synthetic envelope matches expected energy peaks": a frame whose window overlaps a known
        // click's burst must have a far higher normalized energy than a frame squarely inside the
        // silent gap between two clicks.
        // Overlapping windows (HopSamples < FftSize) mean more than one frame can contain a given
        // sample index, so pick any one covering frame rather than requiring exactly one.
        var beatIntervalSamples = (long)(60.0 / 120 * SampleRate); // 4000
        var clickFrame = envelope.Frames.First(f => f.StartSample <= beatIntervalSamples * 2 && beatIntervalSamples * 2 < f.EndSample);
        var gapSample = (beatIntervalSamples * 2) + (beatIntervalSamples / 2); // squarely between click 2 and click 3
        var gapFrame = envelope.Frames.First(f => f.StartSample <= gapSample && gapSample < f.EndSample);

        Assert.True(clickFrame.NormalizedEnergy > gapFrame.NormalizedEnergy,
            $"expected a click-burst frame ({clickFrame.NormalizedEnergy}) to carry more normalized energy than a silent-gap frame ({gapFrame.NormalizedEnergy}).");
        Assert.Equal(0f, gapFrame.NormalizedEnergy);

        // "Unit normalization": the loudest frame is exactly 1.0, and no frame exceeds that ceiling.
        Assert.Contains(envelope.Frames, f => f.NormalizedEnergy == 1.0f);
        Assert.All(envelope.Frames, f => Assert.InRange(f.NormalizedEnergy, 0f, 1f));

        // Provenance recorded (Section 4's "every cache key includes algorithm/model/settings versions").
        Assert.Equal(asset.Sha256, envelope.SourceHash);
        Assert.Equal(asset.DecoderVersion, envelope.SourceDecoderVersion);
        Assert.Equal(EnergyEnvelopeAnalyzer.Version, envelope.AnalyzerVersion);
        Assert.Equal(Settings, envelope.Settings);

        // "Cache reopens without recompute": re-running against the same unchanged asset/settings
        // returns an equivalent envelope without a second real decode.
        var envelope2 = await analyzer.AnalyzeAsync(asset, Settings, CancellationToken.None);
        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.Equal(envelope.VersionId, envelope2.VersionId);
        Assert.Equal(envelope.Frames.Length, envelope2.Frames.Length);
    }

    [Fact]
    public async Task AnalyzeAsync_SilentAudio_ComputesZeroEnergyWithoutThrowing_HandlesSilenceExplicitly()
    {
        // "compare to reference numbers for synthetic tones and silence."
        var silentSamples = new float[SampleRate * 2]; // 2 seconds of true digital silence.
        var asset = await ProbeAsync("silence.wav", silentSamples);

        var analyzer = new EnergyEnvelopeAnalyzer(new WavAudioDecoder(), new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache")));
        var envelope = await analyzer.AnalyzeAsync(asset, Settings, CancellationToken.None);

        Assert.NotEmpty(envelope.Frames);
        Assert.All(envelope.Frames, f =>
        {
            Assert.Equal(0f, f.Rms);
            // Never an undefined 0/0 — a fully silent peak normalizes every frame to exactly 0, not NaN.
            Assert.Equal(0f, f.NormalizedEnergy);
            Assert.False(float.IsNaN(f.NormalizedEnergy));
        });
    }

    [Fact]
    public async Task AnalyzeAsync_MismatchedAnalysisSampleRate_ThrowsWithoutDecodingOrWriting_InvalidInputNegativePath()
    {
        // "Invalid input... must not corrupt the project": a requested analysis rate the decoder
        // cannot actually produce (IAudioDecoder does not resample) must fail before any decode or
        // cache write, never silently analyze at the wrong rate.
        var asset = await ProbeClickTrackAsync("rate-mismatch.wav", bpm: 120, clickCount: 2);
        var mismatchedSettings = Settings with { AnalysisSampleRate = SampleRate * 2 };

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache"));
        var analyzer = new EnergyEnvelopeAnalyzer(decoder, store);

        var ex = await Assert.ThrowsAsync<EnergyEnvelopeInvalidException>(
            () => analyzer.AnalyzeAsync(asset, mismatchedSettings, CancellationToken.None));

        Assert.Contains("AnalysisSampleRate", ex.Reason);
        Assert.Equal(0, decoder.DecodeCallCount);
        Assert.False(File.Exists(store.GetPath(asset.Id)));
    }

    [Fact]
    public async Task AnalyzeAsync_DeclaredSampleCountMismatch_ThrowsEnergyEnvelopeInvalidException_NegativePath()
    {
        var asset = await ProbeClickTrackAsync("count-mismatch.wav", bpm: 120, clickCount: 2);
        var doctored = asset with { SampleCount = asset.SampleCount + 1000 };

        var store = new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache"));
        var analyzer = new EnergyEnvelopeAnalyzer(new WavAudioDecoder(), store);

        var ex = await Assert.ThrowsAsync<EnergyEnvelopeInvalidException>(
            () => analyzer.AnalyzeAsync(doctored, Settings, CancellationToken.None));

        Assert.Contains("sample count", ex.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(store.GetPath(doctored.Id)));
    }

    [Fact]
    public async Task AnalyzeAsync_CancelledBeforeDecodeCompletes_ThrowsWithoutWritingAnyCacheFile_CancellationNegativePath()
    {
        // "Cancellation... must not corrupt the project": a token already cancelled before the
        // decode loop can finish must surface as a cancellation and leave no partial cache file.
        var asset = await ProbeClickTrackAsync("cancel.wav", bpm: 120, clickCount: 20);

        var store = new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache"));
        var analyzer = new EnergyEnvelopeAnalyzer(new WavAudioDecoder(), store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => analyzer.AnalyzeAsync(asset, Settings, cts.Token));

        Assert.False(File.Exists(store.GetPath(asset.Id)));
    }

    [Fact]
    public async Task AnalyzeAsync_ContentHashChanges_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        // "Stale persisted state must not corrupt the project": a re-probe under the same SongId but
        // different content hash (edited audio) must never silently reuse the old envelope.
        var asset = await ProbeClickTrackAsync("hash-change.wav", bpm: 120, clickCount: 4);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache"));
        var analyzer = new EnergyEnvelopeAnalyzer(decoder, store);

        var envelope1 = await analyzer.AnalyzeAsync(asset, Settings, CancellationToken.None);
        Assert.Equal(1, decoder.DecodeCallCount);

        var changed = asset with { Sha256 = "0000000000000000000000000000000000000000000000000000000000000" };
        var envelope2 = await analyzer.AnalyzeAsync(changed, Settings, CancellationToken.None);

        Assert.Equal(2, decoder.DecodeCallCount); // a real second decode/rebuild happened.
        Assert.NotEqual(envelope1.VersionId, envelope2.VersionId);
        Assert.Equal(changed.Sha256, envelope2.SourceHash);

        var reopened = await store.LoadAsync(store.GetPath(asset.Id), CancellationToken.None);
        Assert.Equal(changed.Sha256, reopened.SourceHash);
    }

    [Fact]
    public async Task AnalyzeAsync_SettingsChange_InvalidatesCacheAndRebuilds_StalePersistedStateNegativePath()
    {
        // Same song/content, different windowing — must not silently serve an envelope computed with
        // the previous (now stale) RhythmSettings.
        var asset = await ProbeClickTrackAsync("settings-change.wav", bpm: 120, clickCount: 4);

        var decoder = new CountingAudioDecoder(new WavAudioDecoder());
        var store = new JsonEnergyEnvelopeStore(Path.Combine(_tempDir, "energy-cache"));
        var analyzer = new EnergyEnvelopeAnalyzer(decoder, store);

        var envelope1 = await analyzer.AnalyzeAsync(asset, Settings, CancellationToken.None);
        var widerWindow = Settings with { FftSize = 512, HopSamples = 256 };
        var envelope2 = await analyzer.AnalyzeAsync(asset, widerWindow, CancellationToken.None);

        Assert.Equal(2, decoder.DecodeCallCount);
        Assert.Equal(widerWindow, envelope2.Settings);
        Assert.NotEqual(envelope1.Frames.Length, envelope2.Frames.Length);
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
