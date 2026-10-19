using System.Security.Cryptography;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06 Step 040 — "Integrate selected ONNX/native inference via .NET with deterministic
/// preprocessing/postprocessing." Required acceptance (verbatim): "Output stem count/shape and audio
/// sync assertions pass." Failure/edge case (verbatim, shared across this phase's steps): "Missing or
/// incompatible model, cancellation and offline mode must yield a recoverable state." Implementation
/// guidance (verbatim): "regression-compare with source or ground-truth stems."
///
/// Both ONNX fixtures below are embedded as base64 copies of the real, committed files at
/// <c>fixtures/onnx/synthetic-stem-separator-v1.onnx</c> and
/// <c>fixtures/onnx/synthetic-stem-separator-bad-shape-v1.onnx</c> (sha256 values asserted in
/// <see cref="FixtureHashesMatchCommittedFixtureFiles"/> below) — in-memory embedding matches this
/// project's own established Step 039 precedent ("hand-constructed, in-memory fixture byte payloads
/// ... standing in for real model weights"), so these tests never depend on the test runner's current
/// working directory to locate a file under <c>fixtures/</c>. Neither graph is a real stem-separation
/// model: each is a hand-authored, analytically-invertible "stem = input * known_gain" transform,
/// disclosed in <c>fixtures/FIXTURE-CORPUS-MANIFEST.json</c> and
/// <c>implementation-evidence/PHASE-06/STEP-040.md</c> — no separation-quality claim is made here
/// (Step 044's own disclosed scope); this step proves the ONNX Runtime integration/preprocessing/
/// postprocessing mechanism itself.
/// </summary>
public sealed class Step040AcceptanceTests : IDisposable
{
    private const string GoodFixtureBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjrzAQorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwogCgppbnB1dF91bnNxCgVnYWlucxIGb3V0cHV0IgNNdWwSGFN5bnRoZXRpY1N0ZW1TZXBhcmF0b3JWMSojCAEIBAgBCAEQAUIFZ2FpbnNKEM3MzD0AAIA+AAAAPwAAQD9aLgoFaW5wdXQSJQojCAESHwoCCAEKChIIY2hhbm5lbHMKDRILbnVtX3NhbXBsZXNiMwoGb3V0cHV0EikKJwgBEiMKAggBCgIIBAoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private const string BadShapeFixtureBase64 =
        "CAcSM3hsaWdodHMtYWktc3R1ZGlvLXBoYXNlMDYtc3RlcDA0MC1maXh0dXJlLWdlbmVyYXRvcjq1AgorCgVpbnB1dBIKaW5wdXRfdW5zcSIJVW5zcXVlZXplKgsKBGF4ZXNAAaABBwohCgppbnB1dF91bnNxCgVnYWlucxIHbXVsX291dCIDTXVsCjsKB211bF9vdXQSBm91dHB1dCIKUmVkdWNlTWVhbioLCgRheGVzQAGgAQcqDwoIa2VlcGRpbXMYAKABAhIgU3ludGhldGljU3RlbVNlcGFyYXRvckJhZFNoYXBlVjEqIwgBCAQIAQgBEAFCBWdhaW5zShDNzMw9AACAPgAAAD8AAEA/Wi4KBWlucHV0EiUKIwgBEh8KAggBCgoSCGNoYW5uZWxzCg0SC251bV9zYW1wbGVzYi8KBm91dHB1dBIlCiMIARIfCgIIAQoKEghjaGFubmVscwoNEgtudW1fc2FtcGxlc0IECgAQCw==";

    private static readonly float[] GoodFixtureGains = { 0.10f, 0.25f, 0.50f, 0.75f };

    private static readonly IReadOnlyList<StemKind> FixtureStemOrder =
        new[] { StemKind.Drums, StemKind.Bass, StemKind.Vocals, StemKind.Other };

    private readonly string _rootDirectory;
    private readonly string _modelCacheDirectory;
    private readonly string _audioDirectory;
    private readonly string _stemOutputDirectory;

    public Step040AcceptanceTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step040-{Guid.NewGuid():N}");
        _modelCacheDirectory = Path.Combine(_rootDirectory, "models");
        _audioDirectory = Path.Combine(_rootDirectory, "audio");
        _stemOutputDirectory = Path.Combine(_rootDirectory, "stems");
        Directory.CreateDirectory(_modelCacheDirectory);
        Directory.CreateDirectory(_audioDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void FixtureHashesMatchCommittedFixtureFiles()
    {
        var goodBytes = Convert.FromBase64String(GoodFixtureBase64);
        var badBytes = Convert.FromBase64String(BadShapeFixtureBase64);

        Assert.Equal(
            "5fc4b43c4121faa57751ee5b3d8d4a17601f46c7e39f2501308dcff65a100a75",
            Convert.ToHexString(SHA256.HashData(goodBytes)).ToLowerInvariant());
        Assert.Equal(
            "8d2ab6c9a60d7cdcd8bf856f820d13f291788055caadea6c1e7a57a0bad7efe9",
            Convert.ToHexString(SHA256.HashData(badBytes)).ToLowerInvariant());
    }

    private static StemSeparationRecipe FixtureRecipe(string outputTensorName = "output") => new(
        RecipeVersion: "step040-fixture-recipe/1.0.0",
        ExpectedSampleRate: 44100,
        ExpectedChannels: 1,
        StemOrder: FixtureStemOrder,
        InputTensorName: "input",
        OutputTensorName: outputTensorName);

    private static ModelAsset FixtureModelAsset(byte[] modelBytes, string id = "synthetic-stem-separator") => new(
        Id: id,
        Version: "1.0.0-fixture",
        Source: new Uri("https://example.invalid/models/synthetic-stem-separator.onnx"),
        Sha256: Convert.ToHexString(SHA256.HashData(modelBytes)).ToLowerInvariant(),
        LicenseId: "fixture-only, no third-party rights",
        InferenceBackend: "Microsoft.ML.OnnxRuntime (CPU)");

    private async Task<string> SeedVerifiedModelAsync(ModelAssetManager manager, ModelAsset asset, byte[] modelBytes)
    {
        var verifiedPath = manager.GetVerifiedPath(asset);
        await File.WriteAllBytesAsync(verifiedPath, modelBytes);
        return verifiedPath;
    }

    private async Task<SongAsset> WriteFixtureSongAsync(string fileName, float[] samples, int sampleRate)
    {
        var path = Path.Combine(_audioDirectory, fileName);
        await StemWavWriter.WriteMonoFloatAsync(path, samples, sampleRate, CancellationToken.None);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    private static float[] DeterministicSineWave(int sampleCount, int sampleRate, double frequencyHz, float amplitude)
    {
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = amplitude * (float)Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate);
        }

        return samples;
    }

    /// <summary>Never-invoked sentinel downloader -- the model is always pre-provisioned/offline in these tests.</summary>
    private sealed class NeverCalledDownloader : IModelAssetDownloader
    {
        public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
            throw new InvalidOperationException("Downloader must not be invoked; Step 040 tests always pre-provision the verified model file.");
    }

    [Fact]
    public async Task Step040_Acceptance_Passes_StemCountShapeAndAudioSyncAndGroundTruthRegression()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(sampleCount: 2000, sampleRate, frequencyHz: 220.0, amplitude: 0.6f);
        var song = await WriteFixtureSongAsync("source.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), FixtureRecipe(), _stemOutputDirectory);
        var progressReports = new List<SeparationProgress>();
        var progress = new Progress<SeparationProgress>(p => progressReports.Add(p));

        var manifest = await separator.SeparateAsync(song, asset, progress, CancellationToken.None);

        // Required acceptance, verbatim: "Output stem count/shape and audio sync assertions pass."
        Assert.Equal(FixtureStemOrder.Count, manifest.Stems.Count);
        Assert.Equal(song.Sha256, manifest.OriginalSongHash);
        Assert.Equal(asset.Id, manifest.ModelId);
        Assert.Empty(manifest.Warnings); // mono source, mono recipe -- no disclosed downmix warning expected

        for (var i = 0; i < manifest.Stems.Count; i++)
        {
            var stem = manifest.Stems[i];
            Assert.Equal(FixtureStemOrder[i], stem.Kind);
            Assert.True(File.Exists(stem.FilePath));

            // Audio sync assertion: every stem stays exactly aligned with the source -- same sample
            // count, same declared origin.
            Assert.Equal(song.SampleCount, stem.SampleCount);
            Assert.Equal(song.OriginTicks, stem.SourceOriginTicks);
            Assert.Equal(sampleRate, stem.SampleRate);

            var actualHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(stem.FilePath))).ToLowerInvariant();
            Assert.Equal(actualHash, stem.Sha256);

            // Regression-compare against the known ground truth (implementation guidance, verbatim):
            // this fixture model's exact, known transform is "stem = input * gain[i]"; dividing back
            // out must exactly recover the original source signal (lossless float32 WAV round trip).
            var recoveredSong = await new WavAudioDecoder().ProbeAsync(stem.FilePath, CancellationToken.None);
            var recovered = new List<float>();
            await foreach (var block in new WavAudioDecoder().DecodeMonoAsync(recoveredSong, sampleRate, CancellationToken.None))
            {
                recovered.AddRange(block.Span.ToArray());
            }

            Assert.Equal(input.Length, recovered.Count);
            for (var n = 0; n < input.Length; n++)
            {
                Assert.True(
                    Math.Abs((recovered[n] / GoodFixtureGains[i]) - input[n]) < 1e-4f,
                    $"stem {i} sample {n}: expected ~{input[n]} after dividing by gain {GoodFixtureGains[i]}, got {recovered[n] / GoodFixtureGains[i]}");
            }
        }

        Assert.Contains(progressReports, p => p.Stage == "Complete" && Math.Abs(p.Fraction - 1.0) < 1e-9);
    }

    [Fact]
    public async Task Step040_Acceptance_Passes_SilentSourceYieldsFiniteNoNaNOutput()
    {
        const int sampleRate = 44100;
        var silence = new float[1000]; // all zeros
        var song = await WriteFixtureSongAsync("silence.wav", silence, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), FixtureRecipe(), _stemOutputDirectory);
        var manifest = await separator.SeparateAsync(song, asset, progress: null, CancellationToken.None);

        Assert.Equal(FixtureStemOrder.Count, manifest.Stems.Count);
        foreach (var stem in manifest.Stems)
        {
            var recoveredSong = await new WavAudioDecoder().ProbeAsync(stem.FilePath, CancellationToken.None);
            await foreach (var block in new WavAudioDecoder().DecodeMonoAsync(recoveredSong, sampleRate, CancellationToken.None))
            {
                foreach (var sample in block.Span)
                {
                    Assert.False(float.IsNaN(sample), "silent source must never produce NaN output");
                    Assert.True(float.IsFinite(sample), "silent source must never produce non-finite output");
                    Assert.Equal(0f, sample); // exact: silence * any gain is still exactly silence
                }
            }
        }
    }

    [Fact]
    public async Task SeparateAsync_IncompatibleSampleRate_ThrowsStemSeparationIncompatibleSourceException_NegativePath()
    {
        const int wrongSampleRate = 22050; // recipe declares 44100
        var input = DeterministicSineWave(500, wrongSampleRate, 110.0, 0.3f);
        var song = await WriteFixtureSongAsync("wrong-rate.wav", input, wrongSampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes);
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), FixtureRecipe(), _stemOutputDirectory);

        var ex = await Assert.ThrowsAsync<StemSeparationIncompatibleSourceException>(
            () => separator.SeparateAsync(song, asset, progress: null, CancellationToken.None));

        Assert.Contains("22050", ex.Message);
        Assert.False(Directory.Exists(_stemOutputDirectory) && Directory.GetFiles(_stemOutputDirectory).Length > 0,
            "no stem file must be written when the source is rejected as incompatible");
    }

    [Fact]
    public async Task SeparateAsync_ModelOutputWrongShape_ThrowsStemSeparationOutputShapeException_NegativePath()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(500, sampleRate, 110.0, 0.3f);
        var song = await WriteFixtureSongAsync("source-bad-shape.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(BadShapeFixtureBase64);
        var asset = FixtureModelAsset(modelBytes, id: "synthetic-stem-separator-bad-shape");
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), FixtureRecipe(), _stemOutputDirectory);

        var ex = await Assert.ThrowsAsync<StemSeparationOutputShapeException>(
            () => separator.SeparateAsync(song, asset, progress: null, CancellationToken.None));

        Assert.Contains("rank", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeparateAsync_MissingModelNoConsent_ThrowsModelAssetConsentRequiredException_NegativePath()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(500, sampleRate, 110.0, 0.3f);
        var song = await WriteFixtureSongAsync("source-missing-model.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes, id: "never-provisioned-model");
        // Deliberately never seeded at the verified path, and the downloader must never be invoked:
        // this proves OnnxStemSeparator surfaces Step 039's own consent-gating mechanism rather than
        // silently bypassing or duplicating it.
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), FixtureRecipe(), _stemOutputDirectory);

        await Assert.ThrowsAsync<ModelAssetConsentRequiredException>(
            () => separator.SeparateAsync(song, asset, progress: null, CancellationToken.None));
    }

    [Fact]
    public async Task SeparateAsync_AlreadyCancelled_ThrowsOperationCanceledException_NegativePath()
    {
        const int sampleRate = 44100;
        var input = DeterministicSineWave(500, sampleRate, 110.0, 0.3f);
        var song = await WriteFixtureSongAsync("source-cancelled.wav", input, sampleRate);

        var modelBytes = Convert.FromBase64String(GoodFixtureBase64);
        var asset = FixtureModelAsset(modelBytes, id: "cancel-test-model");
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        await SeedVerifiedModelAsync(manager, asset, modelBytes);

        var separator = new OnnxStemSeparator(manager, new WavAudioDecoder(), FixtureRecipe(), _stemOutputDirectory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => separator.SeparateAsync(song, asset, progress: null, cts.Token));

        Assert.False(Directory.Exists(_stemOutputDirectory) && Directory.GetFiles(_stemOutputDirectory).Length > 0,
            "no stem file must be written when the call is already cancelled");
    }

    [Fact]
    public void Constructor_RecipeDeclaresMultiChannel_ThrowsStemSeparationRecipeInvalidException_NegativePath()
    {
        var manager = new ModelAssetManager(_modelCacheDirectory, new NeverCalledDownloader());
        var stereoRecipe = FixtureRecipe() with { ExpectedChannels = 2 };

        Assert.Throws<StemSeparationRecipeInvalidException>(
            () => new OnnxStemSeparator(manager, new WavAudioDecoder(), stereoRecipe, _stemOutputDirectory));
    }
}
