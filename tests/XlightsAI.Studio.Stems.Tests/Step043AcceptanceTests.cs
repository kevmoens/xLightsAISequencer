using System.Security.Cryptography;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 043 — "Detect instrument activity segments with scores." Required acceptance
/// (verbatim): "Known quiet/active regions appear correctly on timeline." Implementation guidance
/// (verbatim): "Calculate activity using stem RMS/onset features with hysteresis and confidence; avoid
/// treating bleed as precise instrument classification." Failure/edge case (shared phase-wide
/// boilerplate, verbatim): "Malformed proposal, no-improvement and exhausted retry budget must not
/// auto-commit" — reinterpreted for this step's own activity-detection boundary: malformed settings/
/// stale stems must fail before any decode or cache write, and a detected result is never anything more
/// than a read-only, inspectable proposal — it never mutates the source <see cref="StemAsset"/>/
/// <see cref="StemManifest"/> or any accepted document, and a repeated run against unchanged input is
/// fully deterministic (no "retry budget" concept needed because there is nothing probabilistic to
/// retry).
///
/// Exercises <see cref="StemActivityDetector"/> (the real, file-backed detector) directly against real
/// on-disk WAV stems written by the already-real <see cref="StemWavWriter"/> — no ONNX/model fixture is
/// needed for this step's own scope (detecting activity in an already-produced stem, not separating
/// one).
/// </summary>
public sealed class Step043AcceptanceTests : IDisposable
{
    private const int SampleRate = 8000;
    private const long OriginTicks = 50_000L;
    private const long TicksPerSecond = 10_000_000L;

    private readonly string _rootDirectory;
    private readonly string _audioDirectory;
    private readonly string _cacheDirectory;

    public Step043AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step043-{Guid.NewGuid():N}");
        _audioDirectory = Path.Combine(_rootDirectory, "stems");
        _cacheDirectory = Path.Combine(_rootDirectory, "activity-cache");
        Directory.CreateDirectory(_audioDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private static StemActivityDetectionSettings DefaultSettings() => new(
        WindowSamples: 80,
        HopSamples: 80,
        OnThreshold: 0.5,
        OffThreshold: 0.2,
        MinActiveDurationTicks: 0);

    private async Task<StemAsset> WriteStemAsync(StemKind kind, float[] samples, long originTicks = OriginTicks)
    {
        var path = Path.Combine(_audioDirectory, $"{kind}-{Guid.NewGuid():N}.wav");
        await StemWavWriter.WriteMonoFloatAsync(path, samples, SampleRate, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(path);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new StemAsset(Guid.NewGuid(), kind, path, SampleRate, samples.Length, originTicks, sha256);
    }

    /// <summary>Quiet-loud-quiet-loud-quiet: four known, unambiguous regions at fixed sample boundaries.</summary>
    private static float[] QuietLoudQuietLoudQuiet(int quietFrames, int loudFrames, int windowSamples)
    {
        var quiet = new float[quietFrames * windowSamples];
        var loud = Enumerable.Range(0, loudFrames * windowSamples)
            .Select(i => 0.9f * (float)Math.Sin(2 * Math.PI * 220.0 * i / SampleRate))
            .ToArray();

        return quiet.Concat(loud).Concat(quiet).Concat(loud).Concat(quiet).ToArray();
    }

    [Fact]
    public async Task Step043_Acceptance_Passes_KnownQuietAndActiveRegionsAppearCorrectlyOnTimeline()
    {
        var settings = DefaultSettings();
        const int quietFrames = 5;
        const int loudFrames = 10;
        var samples = QuietLoudQuietLoudQuiet(quietFrames, loudFrames, settings.WindowSamples);
        var stem = await WriteStemAsync(StemKind.Drums, samples);

        var detector = new StemActivityDetector(new WavAudioDecoder(), settings);
        var activities = await detector.DetectAsync(stem, CancellationToken.None);

        Assert.Equal(2, activities.Count);

        var firstLoudStartSample = quietFrames * settings.WindowSamples;
        var firstLoudEndSample = firstLoudStartSample + (loudFrames * settings.WindowSamples);
        var secondLoudStartSample = firstLoudEndSample + (quietFrames * settings.WindowSamples);
        var secondLoudEndSample = secondLoudStartSample + (loudFrames * settings.WindowSamples);

        long ToTicks(long sample) => OriginTicks + (long)Math.Round(sample * (TicksPerSecond / (double)SampleRate));

        Assert.Equal(StemKind.Drums, activities[0].Kind);
        Assert.Equal(ToTicks(firstLoudStartSample), activities[0].StartTicks);
        Assert.Equal(ToTicks(firstLoudEndSample), activities[0].EndTicks);
        Assert.True(activities[0].Score > 0.5, $"expected a high confidence score for the loud region, got {activities[0].Score}");

        Assert.Equal(ToTicks(secondLoudStartSample), activities[1].StartTicks);
        Assert.Equal(ToTicks(secondLoudEndSample), activities[1].EndTicks);

        // Known quiet regions (before/between/after the two loud regions) must never be reported as
        // activity — the required acceptance's own "known quiet... regions appear correctly" half.
        Assert.True(activities[0].StartTicks > ToTicks(0));
        Assert.True(activities[1].StartTicks > activities[0].EndTicks);
        Assert.True(activities[1].EndTicks < ToTicks(samples.Length));
    }

    [Fact]
    public async Task SilentStem_ReturnsEmptyActivity_FiniteNoNaN_NegativePath()
    {
        var settings = DefaultSettings();
        var samples = new float[settings.WindowSamples * 20];
        var stem = await WriteStemAsync(StemKind.Vocals, samples);

        var detector = new StemActivityDetector(new WavAudioDecoder(), settings);
        var activities = await detector.DetectAsync(stem, CancellationToken.None);

        Assert.Empty(activities);
    }

    [Fact]
    public async Task ShortBlipBelowMinimumDuration_IsDiscarded_NegativePath()
    {
        var settings = DefaultSettings() with { MinActiveDurationTicks = TicksPerSecond };
        // A single loud window surrounded by quiet -- far shorter than one second at this sample rate.
        var samples = QuietLoudQuietLoudQuiet(quietFrames: 5, loudFrames: 1, settings.WindowSamples);
        var stem = await WriteStemAsync(StemKind.Bass, samples);

        var detector = new StemActivityDetector(new WavAudioDecoder(), settings);
        var activities = await detector.DetectAsync(stem, CancellationToken.None);

        Assert.Empty(activities);
    }

    [Theory]
    [InlineData(0, 80, 0.5, 0.2, 0)]      // WindowSamples not positive
    [InlineData(80, 0, 0.5, 0.2, 0)]      // HopSamples not positive
    [InlineData(80, 80, 0.0, 0.2, 0)]     // OnThreshold not in (0,1]
    [InlineData(80, 80, 1.5, 0.2, 0)]     // OnThreshold not in (0,1]
    [InlineData(80, 80, 0.5, 0.5, 0)]     // OffThreshold not < OnThreshold
    [InlineData(80, 80, 0.5, -0.1, 0)]    // OffThreshold negative
    public void InvalidSettings_ThrowsBeforeConstruction_NegativePath(int window, int hop, double onThreshold, double offThreshold, long minDurationTicks)
    {
        var settings = new StemActivityDetectionSettings(window, hop, onThreshold, offThreshold, minDurationTicks);

        Assert.Throws<StemActivityDetectionInvalidException>(
            () => new StemActivityDetector(new WavAudioDecoder(), settings));
    }

    [Fact]
    public async Task StemFileModifiedAfterManifest_ThrowsStemActivityDetectionInvalidException_NegativePath()
    {
        var settings = DefaultSettings();
        var stem = await WriteStemAsync(StemKind.Vocals, QuietLoudQuietLoudQuiet(5, 10, settings.WindowSamples));

        // Overwrite the real file content without updating the declared manifest hash/sample count --
        // a stale/corrupt stem, exactly like Step 042's own StemSongAssetFactory cross-check.
        await StemWavWriter.WriteMonoFloatAsync(stem.FilePath, new float[10], SampleRate, CancellationToken.None);

        var detector = new StemActivityDetector(new WavAudioDecoder(), settings);

        await Assert.ThrowsAsync<StemActivityDetectionInvalidException>(
            () => detector.DetectAsync(stem, CancellationToken.None));
    }

    [Fact]
    public async Task DetectAsync_AlreadyCancelled_ThrowsOperationCanceledException_NoCacheFile_NegativePath()
    {
        var settings = DefaultSettings();
        var stem = await WriteStemAsync(StemKind.Drums, QuietLoudQuietLoudQuiet(5, 10, settings.WindowSamples));
        var store = new JsonStemActivityResultStore(_cacheDirectory);
        var detector = new StemActivityDetector(new WavAudioDecoder(), settings, store);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => detector.DetectAsync(stem, cts.Token));

        Assert.False(File.Exists(store.GetPath(stem.Id)), "no activity cache file must be written when the call is already cancelled");
    }

    [Fact]
    public async Task DetectAsync_SecondCall_ReusesCacheEvenAfterStemFileIsDeleted()
    {
        var settings = DefaultSettings();
        var stem = await WriteStemAsync(StemKind.Drums, QuietLoudQuietLoudQuiet(5, 10, settings.WindowSamples));
        var store = new JsonStemActivityResultStore(_cacheDirectory);
        var detector = new StemActivityDetector(new WavAudioDecoder(), settings, store);

        var first = await detector.DetectAsync(stem, CancellationToken.None);

        File.Delete(stem.FilePath);

        var second = await detector.DetectAsync(stem, CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task DetectAsync_IsDeterministic_AndNeverMutatesTheSourceStemFile()
    {
        var settings = DefaultSettings();
        var stem = await WriteStemAsync(StemKind.Guitar, QuietLoudQuietLoudQuiet(5, 10, settings.WindowSamples));
        var bytesBefore = await File.ReadAllBytesAsync(stem.FilePath);

        var detector = new StemActivityDetector(new WavAudioDecoder(), settings);

        var first = await detector.DetectAsync(stem, CancellationToken.None);
        var second = await detector.DetectAsync(stem, CancellationToken.None);

        Assert.Equal(first, second);

        var bytesAfter = await File.ReadAllBytesAsync(stem.FilePath);
        Assert.Equal(bytesBefore, bytesAfter);
    }
}
