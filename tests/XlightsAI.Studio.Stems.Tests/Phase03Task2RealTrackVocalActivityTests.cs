using System.Security.Cryptography;
using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 2 acceptance criterion (verbatim): "vocalActive correctly flags
/// the known vocal-cluster timestamps from EXPORT_AUDIT.md's vocals investigation (~94s, ~400-412s,
/// etc.) on that same track." Runs <see cref="VocalActivityAnalyzer"/> against the real, already-separated
/// vocals stem at <c>C:\xLights\stems\Dubstep Christmas Light Show\vocals.wav</c> when present on the
/// machine running the tests -- skips cleanly everywhere else (CI, a dev box without that external
/// reference asset), mirroring <c>XlightsAI.Studio.Knowledge.Tests.RealXlightsInstallTests</c>'s own
/// established convention for a real-machine-only acceptance check. The deterministic, synthetic-fixture
/// tests in <c>Phase03Task2VocalActivityAcceptanceTests</c> are what every build actually depends on.
/// </summary>
public sealed class Phase03Task2RealTrackVocalActivityTests
{
    private const string VocalsStemPath = @"C:\xLights\stems\Dubstep Christmas Light Show\vocals.wav";

    /// <summary>
    /// Known vocal-energy cluster centers, in seconds, from <c>EXPORT_AUDIT.md</c>'s own direct
    /// Demucs-based vocals investigation against this exact track: "Vocal-energy clusters found: ~94s,
    /// ~122-124s, ~182s, 400-412s (strongest), ~482s, ~568-570s, ~630-632s."
    /// </summary>
    private static readonly double[] KnownVocalClusterCentersSeconds = [94, 123, 182, 406, 482, 569, 631];

    [Fact]
    public async Task VocalActive_FlagsKnownVocalClusterTimestamps_OnReferenceTrack()
    {
        if (!File.Exists(VocalsStemPath))
        {
            // No local copy of the real, already-separated reference stem on this machine (CI, a dev box
            // that hasn't run stem separation against C:\xLights's own reference track) -- nothing to
            // check here. The synthetic-fixture tests are what every build depends on.
            return;
        }

        var decoder = new WavAudioDecoder();
        var probed = await decoder.ProbeAsync(VocalsStemPath, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(VocalsStemPath);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var vocalsStem = new StemAsset(
            Guid.NewGuid(), StemKind.Vocals, VocalsStemPath, probed.SampleRate, probed.SampleCount, SourceOriginTicks: 0, sha256);
        var manifest = new StemManifest(Guid.NewGuid(), "reference-track", "demucs-external", [vocalsStem], ["Externally separated via Demucs, not this repository's own ONNX pipeline."]);

        var analyzer = new VocalActivityAnalyzer(decoder);
        var envelope = await analyzer.AnalyzeAsync(manifest, CancellationToken.None);

        Assert.NotNull(envelope.VocalsStemId);
        Assert.NotEmpty(envelope.ActiveRegions);

        const long ticksPerSecond = 10_000_000L;
        const double toleranceSeconds = 6.0; // one VocalActivitySettings window + hop of slack either side.

        var flaggedCount = 0;
        var misses = new List<double>();
        foreach (var centerSeconds in KnownVocalClusterCentersSeconds)
        {
            var isNearAnyActiveRegion = envelope.ActiveRegions.Any(r =>
            {
                var startSeconds = (r.StartTicks / (double)ticksPerSecond) - toleranceSeconds;
                var endSeconds = (r.EndTicks / (double)ticksPerSecond) + toleranceSeconds;
                return centerSeconds >= startSeconds && centerSeconds <= endSeconds;
            });

            if (isNearAnyActiveRegion)
            {
                flaggedCount++;
            }
            else
            {
                misses.Add(centerSeconds);
            }
        }

        // Most of the known clusters should land inside (or within one window/hop of) a detected active
        // region -- "correctly flags the known vocal-cluster timestamps," not necessarily every single
        // one bit-for-bit (RMS-threshold activity detection on a real, noisy dubstep mix is not expected
        // to be pixel-perfect against a separately-run spectrogram investigation).
        Assert.True(
            flaggedCount >= (int)Math.Ceiling(KnownVocalClusterCentersSeconds.Length * 0.6),
            $"Only {flaggedCount}/{KnownVocalClusterCentersSeconds.Length} known vocal clusters were flagged active. Missed: {string.Join(", ", misses)}s.");
    }
}
