using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;
using XlightsAI.Studio.Rhythm;

namespace XlightsAI.Studio.Rhythm.Tests;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 1 — "expose per-band (bass/mid/high) spectral-flux-derived
/// energy as first-class analysis output instead of leaving it trapped inside OnsetDetector." Required
/// acceptance (build plan, verbatim): "Per-band energy is queryable per time window and visibly
/// differentiates a bass-heavy section from a treble-heavy one on a known test track."
/// </summary>
public sealed class Phase03Task1BandEnergyAcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private static readonly RhythmSettings WindowSettings = new(AnalysisSampleRate: SampleRate, FftSize: 256, HopSamples: 128);
    private static readonly BandEnergySettings DefaultBandSettings = new(WindowSettings, BassCutoffHz: 250, HighCutoffHz: 2000);
    private const long TicksPerSecond = 10_000_000L;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-phase03-task1-{Guid.NewGuid():N}");

    public Phase03Task1BandEnergyAcceptanceTests()
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

    private static long ToTicks(long sampleIndex) => (long)Math.Round(sampleIndex * (TicksPerSecond / (double)SampleRate));

    /// <summary>Quiet/loud bursts at a single frequency — bursts (not a sustained tone) so each onset produces real spectral-flux energy to measure, the same convention Step032's own fixtures use.</summary>
    private static float[] BurstsAt(double toneHz, int burstCount, int quietSamples, int loudSamples)
    {
        var segments = new List<float>();
        for (var i = 0; i < burstCount; i++)
        {
            segments.AddRange(new float[quietSamples]);
            segments.AddRange(Enumerable.Range(0, loudSamples)
                .Select(n => 0.9f * (float)Math.Sin(2 * Math.PI * toneHz * n / SampleRate)));
        }

        return segments.ToArray();
    }

    [Fact]
    public async Task BandEnergy_DifferentiatesBassHeavySectionFromTrebleHeavySection()
    {
        // Known test track: a bass-heavy section (100 Hz bursts, well inside the bass band) followed by a
        // treble-heavy section (3500 Hz bursts, well inside the high band).
        var bassSection = BurstsAt(toneHz: 100, burstCount: 6, quietSamples: 400, loudSamples: 400);
        var trebleSection = BurstsAt(toneHz: 3500, burstCount: 6, quietSamples: 400, loudSamples: 400);
        var samples = bassSection.Concat(trebleSection).ToArray();

        var asset = await ProbeAsync("band-energy.wav", samples);
        var analyzer = new BandEnergyAnalyzer(new WavAudioDecoder());

        var envelope = await analyzer.AnalyzeAsync(asset, DefaultBandSettings, CancellationToken.None);

        Assert.NotEmpty(envelope.Frames);
        Assert.Equal(BandEnergyAnalyzer.Version, envelope.AnalyzerVersion);

        var bassWindow = (0L, ToTicks(bassSection.Length));
        var trebleWindow = (ToTicks(bassSection.Length), ToTicks(samples.Length));

        var (bassWindowBass, _, bassWindowHigh) = envelope.AverageInWindow(bassWindow.Item1, bassWindow.Item2);
        var (trebleWindowBass, _, trebleWindowHigh) = envelope.AverageInWindow(trebleWindow.Item1, trebleWindow.Item2);

        // Per time window: the bass-heavy section's own bass energy clearly dominates its own high
        // energy, and the treble-heavy section's own high energy clearly dominates its own bass energy.
        Assert.True(bassWindowBass > bassWindowHigh,
            $"bass section: expected Bass ({bassWindowBass}) > High ({bassWindowHigh}).");
        Assert.True(trebleWindowHigh > trebleWindowBass,
            $"treble section: expected High ({trebleWindowHigh}) > Bass ({trebleWindowBass}).");

        // And across sections: the bass-heavy section carries more bass energy than the treble-heavy one,
        // and vice versa for high energy -- a bass-heavy section is visibly differentiated from a
        // treble-heavy one, this task's own required acceptance text.
        Assert.True(bassWindowBass > trebleWindowBass);
        Assert.True(trebleWindowHigh > bassWindowHigh);
    }

    [Fact]
    public async Task BandEnergy_SilentSource_YieldsFiniteZeroOutput_NoNaNs()
    {
        var samples = new float[4000];
        var asset = await ProbeAsync("silence.wav", samples);
        var analyzer = new BandEnergyAnalyzer(new WavAudioDecoder());

        var envelope = await analyzer.AnalyzeAsync(asset, DefaultBandSettings, CancellationToken.None);

        Assert.NotEmpty(envelope.Frames);
        Assert.All(envelope.Frames, f =>
        {
            Assert.False(float.IsNaN(f.Bass));
            Assert.False(float.IsNaN(f.Mid));
            Assert.False(float.IsNaN(f.High));
            Assert.Equal(0f, f.Bass);
            Assert.Equal(0f, f.Mid);
            Assert.Equal(0f, f.High);
        });

        var (bass, mid, high) = envelope.AverageInWindow(0, ToTicks(samples.Length));
        Assert.Equal(0.0, bass);
        Assert.Equal(0.0, mid);
        Assert.Equal(0.0, high);
    }

    [Fact]
    public async Task BandEnergy_CachesAndReusesStillValidResult()
    {
        var samples = BurstsAt(toneHz: 100, burstCount: 3, quietSamples: 400, loudSamples: 400);
        var asset = await ProbeAsync("cache.wav", samples);
        var store = new JsonBandEnergyEnvelopeStore(Path.Combine(_tempDir, "band-energy-cache"));
        var decoder = new CountingDecoder(new WavAudioDecoder());
        var analyzer = new BandEnergyAnalyzer(decoder, store);

        var first = await analyzer.AnalyzeAsync(asset, DefaultBandSettings, CancellationToken.None);
        var second = await analyzer.AnalyzeAsync(asset, DefaultBandSettings, CancellationToken.None);

        Assert.Equal(1, decoder.DecodeCallCount);
        Assert.Equal(first.VersionId, second.VersionId);
        Assert.True(File.Exists(store.GetPath(asset.Id)));
    }

    [Theory]
    [InlineData(0, 2000)]
    [InlineData(250, 100)]
    public async Task BandEnergy_InvalidBandCutoffs_ThrowsBandEnergyAnalysisInvalidException(double bassCutoff, double highCutoff)
    {
        var asset = await ProbeAsync("invalid.wav", new float[1000]);
        var analyzer = new BandEnergyAnalyzer(new WavAudioDecoder());
        var badSettings = new BandEnergySettings(WindowSettings, bassCutoff, highCutoff);

        await Assert.ThrowsAsync<BandEnergyAnalysisInvalidException>(
            () => analyzer.AnalyzeAsync(asset, badSettings, CancellationToken.None));
    }

    private sealed class CountingDecoder(IAudioDecoder inner) : IAudioDecoder
    {
        public int DecodeCallCount { get; private set; }

        public Task<SongAsset> ProbeAsync(string path, CancellationToken ct) => inner.ProbeAsync(path, ct);

        public async IAsyncEnumerable<ReadOnlyMemory<float>> DecodeMonoAsync(
            SongAsset song, int targetSampleRate, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            DecodeCallCount++;
            await foreach (var block in inner.DecodeMonoAsync(song, targetSampleRate, ct))
            {
                yield return block;
            }
        }
    }
}
