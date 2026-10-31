using System.Security.Cryptography;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// Build-plan Phase 8 task 3 (singing-face support). <see cref="VisemeApproximator"/> is an amplitude/
/// zero-crossing-rate HEURISTIC, not phoneme recognition (no ASR/transcript exists in this pipeline) --
/// these tests check the mechanical contract (real xLights viseme codes, gated on real vocal activity,
/// a noisy/high-ZCR signal reads as the fricative code) rather than any claim of linguistic accuracy.
/// </summary>
public sealed class VisemeApproximatorTests : IDisposable
{
    private const int SampleRate = 8000;

    private readonly string _rootDirectory;

    public VisemeApproximatorTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-viseme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private async Task<StemAsset> WriteStemAsync(float[] samples)
    {
        var path = Path.Combine(_rootDirectory, $"vocals-{Guid.NewGuid():N}.wav");
        await StemWavWriter.WriteMonoFloatAsync(path, samples, SampleRate, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StemAsset(Guid.NewGuid(), StemKind.Vocals, path, SampleRate, samples.Length, SourceOriginTicks: 0, sha256);
    }

    private static float[] SustainedTone(int sampleCount, double frequencyHz, float amplitude) =>
        Enumerable.Range(0, sampleCount).Select(i => amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate)).ToArray();

    private static float[] WhiteNoise(int sampleCount, float amplitude)
    {
        var random = new Random(1234);
        return Enumerable.Range(0, sampleCount).Select(_ => amplitude * (float)((random.NextDouble() * 2) - 1)).ToArray();
    }

    [Fact]
    public async Task ApproximateAsync_NoActiveRegions_ReturnsEmpty_NeverFabricatesAMark()
    {
        var vocals = await WriteStemAsync(SustainedTone(SampleRate, 220, 0.9f));
        var manifest = new StemManifest(Guid.NewGuid(), "song-hash", "model-id", [vocals], []);
        var envelope = new VocalActivityEnvelope(
            Guid.NewGuid(), vocals.Id, vocals.Sha256, "test", new StemActivityDetectionSettings(1000, 500, 0.1, 0.05, 1_000_000), ActiveRegions: []);

        var approximator = new VisemeApproximator(new WavAudioDecoder());
        var marks = await approximator.ApproximateAsync(manifest, envelope, TimeSpan.FromMilliseconds(125), CancellationToken.None);

        Assert.Empty(marks);
    }

    [Fact]
    public async Task ApproximateAsync_NoiseWindow_PicksTheFricativeCode()
    {
        var noiseSamples = WhiteNoise(SampleRate, 0.5f);
        var vocals = await WriteStemAsync(noiseSamples);
        var manifest = new StemManifest(Guid.NewGuid(), "song-hash", "model-id", [vocals], []);
        var activeRegionTicks = (long)(noiseSamples.Length * (10_000_000.0 / SampleRate));
        var envelope = new VocalActivityEnvelope(
            Guid.NewGuid(), vocals.Id, vocals.Sha256, "test", new StemActivityDetectionSettings(1000, 500, 0.1, 0.05, 1_000_000),
            ActiveRegions: [new StemActivity(StemKind.Vocals, 0, activeRegionTicks, 1.0)]);

        var approximator = new VisemeApproximator(new WavAudioDecoder());
        var marks = await approximator.ApproximateAsync(manifest, envelope, TimeSpan.FromMilliseconds(125), CancellationToken.None);

        Assert.NotEmpty(marks);
        Assert.All(marks, m => Assert.Equal("FV", m.Code));
    }

    [Fact]
    public async Task ApproximateAsync_SustainedToneWithNoVocalsStemMatch_ReturnsEmpty()
    {
        var other = await WriteStemAsync(SustainedTone(SampleRate, 220, 0.9f));
        var manifest = new StemManifest(Guid.NewGuid(), "song-hash", "model-id", [other], []);
        // VocalsStemId deliberately does not match any stem in the manifest.
        var envelope = new VocalActivityEnvelope(
            Guid.NewGuid(), Guid.NewGuid(), "mismatched-hash", "test", new StemActivityDetectionSettings(1000, 500, 0.1, 0.05, 1_000_000),
            ActiveRegions: [new StemActivity(StemKind.Vocals, 0, 10_000_000, 1.0)]);

        var approximator = new VisemeApproximator(new WavAudioDecoder());
        var marks = await approximator.ApproximateAsync(manifest, envelope, TimeSpan.FromMilliseconds(125), CancellationToken.None);

        Assert.Empty(marks);
    }
}
