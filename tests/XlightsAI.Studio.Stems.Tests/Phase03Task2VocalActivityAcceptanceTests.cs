using System.Security.Cryptography;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 2 — "derive a vocalActive envelope from the existing two-stem
/// separation (vocals + accompaniment), despite the six-member StemKind enum only populating two in
/// practice." Exercises <see cref="VocalActivityAnalyzer"/> against real on-disk WAV stems, the same
/// convention Step 043's own <see cref="StemActivityDetector"/> acceptance tests use.
/// </summary>
public sealed class Phase03Task2VocalActivityAcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private const long OriginTicks = 0L;

    private readonly string _rootDirectory;
    private readonly string _audioDirectory;

    public Phase03Task2VocalActivityAcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-phase03-task2-{Guid.NewGuid():N}");
        _audioDirectory = Path.Combine(_rootDirectory, "stems");
        Directory.CreateDirectory(_audioDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private async Task<StemAsset> WriteStemAsync(StemKind kind, float[] samples)
    {
        var path = Path.Combine(_audioDirectory, $"{kind}-{Guid.NewGuid():N}.wav");
        await StemWavWriter.WriteMonoFloatAsync(path, samples, SampleRate, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StemAsset(Guid.NewGuid(), kind, path, SampleRate, samples.Length, OriginTicks, sha256);
    }

    /// <summary>Two well-separated clusters of sustained tone (vocal-like sustained energy), with long silence in between and around -- the same "known quiet/active regions" shape Step 043's own fixtures use.</summary>
    private static float[] TwoClusters(int silenceSamples, int clusterSamples)
    {
        var cluster = Enumerable.Range(0, clusterSamples)
            .Select(i => 0.9f * (float)Math.Sin(2 * Math.PI * 220.0 * i / SampleRate))
            .ToArray();

        var silence = new float[silenceSamples];
        return silence.Concat(cluster).Concat(silence).Concat(cluster).Concat(silence).ToArray();
    }

    [Fact]
    public async Task AnalyzeAsync_VocalsStemPresent_FlagsKnownActiveClustersAsVocalActive()
    {
        // Generous margins relative to VocalActivitySettings' own 2s window/1s hop at this sample rate,
        // so the mid-silence measurement point sits comfortably inside a window containing no loud
        // samples at all, rather than one still overlapping a cluster's own hysteresis tail.
        const int silenceSamples = 60_000; // 7.5 s
        const int clusterSamples = 20_000; // 2.5 s
        var samples = TwoClusters(silenceSamples, clusterSamples);
        var vocals = await WriteStemAsync(StemKind.Vocals, samples);
        var accompaniment = await WriteStemAsync(StemKind.Other, new float[samples.Length]);
        var manifest = new StemManifest(Guid.NewGuid(), "song-hash", "model-id", [vocals, accompaniment], []);

        var analyzer = new VocalActivityAnalyzer(new WavAudioDecoder());
        var envelope = await analyzer.AnalyzeAsync(manifest, CancellationToken.None);

        Assert.Equal(vocals.Id, envelope.VocalsStemId);
        Assert.Equal(vocals.Sha256, envelope.SourceHash);
        Assert.Equal(VocalActivityAnalyzer.Version, envelope.DetectorVersion);
        Assert.Equal(2, envelope.ActiveRegions.Count);
        Assert.All(envelope.ActiveRegions, r => Assert.Equal(StemKind.Vocals, r.Kind));

        // Known active cluster centers (well inside each cluster, not at a hysteresis-sensitive edge).
        var firstClusterCenterTicks = (long)((silenceSamples + (clusterSamples / 2)) * (10_000_000.0 / SampleRate));
        var secondClusterCenterTicks = (long)(((2 * silenceSamples) + (1.5 * clusterSamples)) * (10_000_000.0 / SampleRate));
        Assert.True(envelope.IsActiveAt(firstClusterCenterTicks));
        Assert.True(envelope.IsActiveAt(secondClusterCenterTicks));

        // Known quiet region: the exact middle of the silence gap between the two clusters.
        var midSilenceTicks = (long)((silenceSamples + clusterSamples + (silenceSamples / 2.0)) * (10_000_000.0 / SampleRate));
        Assert.False(envelope.IsActiveAt(midSilenceTicks));
    }

    [Fact]
    public async Task AnalyzeAsync_NoVocalsStem_ReturnsEmptyEnvelope_NeverThrows()
    {
        var accompaniment = await WriteStemAsync(StemKind.Other, new float[8000]);
        var manifest = new StemManifest(Guid.NewGuid(), "song-hash", "model-id", [accompaniment], []);

        var analyzer = new VocalActivityAnalyzer(new WavAudioDecoder());
        var envelope = await analyzer.AnalyzeAsync(manifest, CancellationToken.None);

        Assert.Null(envelope.VocalsStemId);
        Assert.Null(envelope.SourceHash);
        Assert.Empty(envelope.ActiveRegions);
        Assert.False(envelope.IsActiveAt(0));
    }

    [Fact]
    public async Task AnalyzeAsync_NullManifest_ThrowsArgumentNullException()
    {
        var analyzer = new VocalActivityAnalyzer(new WavAudioDecoder());
        await Assert.ThrowsAsync<ArgumentNullException>(() => analyzer.AnalyzeAsync(null!, CancellationToken.None));
    }
}
