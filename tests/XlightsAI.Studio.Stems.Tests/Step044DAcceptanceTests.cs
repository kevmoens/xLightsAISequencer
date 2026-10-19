using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// PHASE-06A Step 044D — "Declare the real sherpa-onnx Spleeter ModelAsset, extract it, and prove a real
/// end-to-end separation run." Required acceptance (verbatim): "A real stereo audio sample is separated
/// into two real stem WAV files via the real downloaded model; byte-for-byte determinism across two runs
/// is proven." Failure/edge case (verbatim): "A tampered/corrupted download (hash mismatch against the
/// pinned, directly-observed value) is rejected before extraction ever runs, reusing
/// ModelAssetManager's own existing ModelAssetHashMismatchException mechanism unmodified; an
/// already-cancelled run never leaves a partial extracted file or partial stem output on disk."
///
/// <b>Real network access required.</b> <see cref="Step044D_Acceptance_Passes_RealModelSeparatesRealStereoSampleDeterministically"/>
/// performs a genuine HTTPS download of the real, 74,682,545-byte <c>sherpa-onnx-spleeter-2stems.tar.bz2</c>
/// archive from <c>github.com</c> on its first run on a given machine (cached thereafter at a stable,
/// non-test-local path so repeat runs reuse the already hash-verified copy instead of re-downloading --
/// disclosed in this step's own evidence file, §3). No model weights, archive bytes, or ONNX inference
/// result are faked, mocked, or pre-computed anywhere in this file.
/// </summary>
public sealed class Step044DAcceptanceTests : IDisposable
{
    /// <summary>
    /// Deliberately NOT deleted in <see cref="Dispose"/> (unlike every other per-test temp directory in
    /// this addendum) -- this is the real, already hash-verified model cache, shared across test runs on
    /// this machine so a genuine network download only ever happens once, not once per test execution.
    /// </summary>
    private static readonly string SharedModelAssetCacheDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-phase06a-step044d-model-asset-cache");

    private readonly string _runDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step044d-{Guid.NewGuid():N}");

    public Step044DAcceptanceTests() => Directory.CreateDirectory(_runDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_runDirectory))
        {
            Directory.Delete(_runDirectory, recursive: true);
        }
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>A real multi-second stereo WAV sample at the real model's own required 44100 Hz -- two
    /// distinct synthetic tones per channel, never a copyrighted recording.</summary>
    private static async Task<SongAsset> WriteRealStereoFixtureAsync(string path)
    {
        const int sampleRate = 44100;
        var left = DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase([220.0, 330.0], noteDurationSeconds: 1.5, sampleRate);
        var right = DeterministicSyntheticAudioGenerator.BuildHarmonicPhrase([440.0, 550.0], noteDurationSeconds: 1.5, sampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeStereoWav(left, right, sampleRate);
        await File.WriteAllBytesAsync(path, wavBytes);

        var decoder = new WavAudioDecoder();
        return await decoder.ProbeAsync(path, CancellationToken.None);
    }

    /// <summary>Real, genuine HTTPS download of the real archive via <see cref="HttpModelAssetDownloader"/>,
    /// cached (hash-reused, never re-downloaded) across every test execution on this machine.</summary>
    private static async Task<ModelAssetManager> EnsureRealModelDownloadedAsync()
    {
        using var downloader = new HttpModelAssetDownloader();
        var manager = new ModelAssetManager(SharedModelAssetCacheDirectory, downloader);
        await manager.EnsureVerifiedAsync(SherpaOnnxSpleeterModelAsset.Archive, userConsent: true, CancellationToken.None);
        return manager;
    }

    [Fact]
    public async Task Step044D_Acceptance_Passes_RealModelSeparatesRealStereoSampleDeterministically()
    {
        // 1. Real, declared ModelAsset -- the pinned, directly-observed hash (never the stale
        // published checksum.txt value), validated by the existing, unmodified ModelAssetValidator.
        var asset = SherpaOnnxSpleeterModelAsset.Archive;
        ModelAssetValidator.Validate(asset);
        Assert.Equal("e26401d9c1801f43c0229731d78d32c2e80085e3aceedeb25f29d2de5fa68ca2", asset.Sha256);

        // 2. Real download + real hash verification (genuine network access the first time this ever
        // runs on a machine; a cached, already-verified re-run thereafter -- see class doc comment).
        var modelAssetManager = await EnsureRealModelDownloadedAsync();
        var archivePath = modelAssetManager.GetVerifiedPath(asset);
        Assert.True(File.Exists(archivePath));
        Assert.Equal(74_682_545L, new FileInfo(archivePath).Length);

        // 3. Real, deterministic tar+bzip2 extraction (SharpCompress-backed, this step's own disclosed
        // dependency) into the real two ONNX files the real model actually ships.
        var expander = new TarBzip2ModelArchiveExpander();
        var extractDirectory = Path.Combine(_runDirectory, "extracted");
        var extracted = await expander.ExpandAsync(archivePath, extractDirectory, CancellationToken.None);

        Assert.Equal(2, extracted.Count);
        var vocalsFile = Assert.Single(extracted, f => f.ArchiveEntryName == "sherpa-onnx-spleeter-2stems/vocals.onnx");
        var accompanimentFile = Assert.Single(extracted, f => f.ArchiveEntryName == "sherpa-onnx-spleeter-2stems/accompaniment.onnx");
        Assert.True(File.Exists(vocalsFile.LocalPath));
        Assert.True(File.Exists(accompanimentFile.LocalPath));
        Assert.True(vocalsFile.Length > 30_000_000);
        Assert.True(accompanimentFile.Length > 30_000_000);

        // 4. Real multi-second stereo audio sample, decoded via the real, already-proven (Step 044A)
        // stereo decoder -- wired through Step 044C's real SpectrogramOnnxStemSeparator.
        var wavPath = Path.Combine(_runDirectory, "real-stereo-sample.wav");
        var source = await WriteRealStereoFixtureAsync(wavPath);
        Assert.Equal(44100, source.SampleRate);
        Assert.Equal(2, source.Channels);

        var separator = new SpectrogramOnnxStemSeparator(
            modelAssetManager,
            expander,
            new WavStereoAudioDecoder(),
            outputDirectory: Path.Combine(_runDirectory, "run1"));
        var separator2 = new SpectrogramOnnxStemSeparator(
            modelAssetManager,
            expander,
            new WavStereoAudioDecoder(),
            outputDirectory: Path.Combine(_runDirectory, "run2"));

        // 5. Required acceptance, verbatim: run the real separation twice and confirm byte-for-byte
        // determinism. Two independent separator instances/output directories so neither run can reuse
        // the other's own written files -- each genuinely recomputes STFT -> ONNX inference -> mask -> ISTFT.
        var manifest1 = await separator.SeparateAsync(source, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe, progress: null, CancellationToken.None);
        var manifest2 = await separator2.SeparateAsync(source, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe, progress: null, CancellationToken.None);

        Assert.Equal(2, manifest1.Stems.Count);
        Assert.Equal(2, manifest2.Stems.Count);
        Assert.Contains(manifest1.Stems, s => s.Kind == StemKind.Vocals);
        Assert.Contains(manifest1.Stems, s => s.Kind == StemKind.Other);

        foreach (var kind in new[] { StemKind.Vocals, StemKind.Other })
        {
            var stem1 = Assert.Single(manifest1.Stems, s => s.Kind == kind);
            var stem2 = Assert.Single(manifest2.Stems, s => s.Kind == kind);

            var bytes1 = await File.ReadAllBytesAsync(stem1.FilePath);
            var bytes2 = await File.ReadAllBytesAsync(stem2.FilePath);

            // Byte-for-byte determinism, verbatim acceptance text -- not merely "close": full file
            // content equality, independently re-hashed here rather than trusting the manifest's own
            // recorded Sha256 field alone.
            Assert.Equal(Sha256Hex(bytes1), Sha256Hex(bytes2));
            Assert.Equal(stem1.Sha256, stem2.Sha256);
            Assert.True(bytes1.Length > 44); // real WAV header + real sample data, never an empty stub
            Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes1, 0, 4));
            Assert.Equal("WAVE", Encoding.ASCII.GetString(bytes1, 8, 4));
        }
    }

    /// <summary>Deterministic in-memory stand-in for a real remote server, reused from Step 039's own
    /// established pattern -- never touches the network.</summary>
    private sealed class FakeModelAssetDownloader : IModelAssetDownloader
    {
        private readonly byte[] _payload;
        public int CallCount { get; private set; }

        private FakeModelAssetDownloader(byte[] payload) => _payload = payload;

        public static FakeModelAssetDownloader ReturningBytes(byte[] payload) => new(payload);

        public async Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct)
        {
            CallCount++;
            await File.WriteAllBytesAsync(destinationPath, _payload, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Throws if ever invoked -- proves extraction never runs on a rejected/tampered asset.</summary>
    private sealed class NeverCalledArchiveExpander : IModelArchiveExpander
    {
        public Task<IReadOnlyList<ExtractedModelFile>> ExpandAsync(string verifiedArchivePath, string destinationDirectory, CancellationToken ct) =>
            throw new InvalidOperationException(
                "IModelArchiveExpander must never be invoked when the declared model asset has not passed hash verification.");
    }

    [Fact]
    public async Task EnsureVerifiedAsync_TamperedDownloadOfTheRealDeclaredAsset_ThrowsModelAssetHashMismatchException_ReusingStep039sMechanismUnmodified_NegativePath()
    {
        var realAsset = SherpaOnnxSpleeterModelAsset.Archive;
        var tamperedPayload = Encoding.UTF8.GetBytes("a tampered substitute for the real sherpa-onnx-spleeter-2stems archive");
        var tamperedDownloader = FakeModelAssetDownloader.ReturningBytes(tamperedPayload);
        var manager = new ModelAssetManager(_runDirectory, tamperedDownloader);

        var ex = await Assert.ThrowsAsync<ModelAssetHashMismatchException>(
            () => manager.EnsureVerifiedAsync(realAsset, userConsent: true, CancellationToken.None));

        Assert.Contains(realAsset.Sha256, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(manager.GetVerifiedPath(realAsset)));
        Assert.Equal(1, tamperedDownloader.CallCount);
    }

    [Fact]
    public async Task SeparateAsync_PreExistingCorruptedCachedArchive_RejectedBeforeExtractionEverRuns_NegativePath()
    {
        var neverCalledDownloader = FakeModelAssetDownloader.ReturningBytes([]);
        var manager = new ModelAssetManager(_runDirectory, neverCalledDownloader);

        // Simulate a previously-downloaded file that has since been corrupted/tampered on disk --
        // placed directly at the manager's own verified path, bypassing any download at all.
        var verifiedPath = manager.GetVerifiedPath(SherpaOnnxSpleeterModelAsset.Archive);
        Directory.CreateDirectory(Path.GetDirectoryName(verifiedPath)!);
        await File.WriteAllBytesAsync(verifiedPath, "corrupted-on-disk-garbage"u8.ToArray());

        var neverCalledExpander = new NeverCalledArchiveExpander();
        var wavPath = Path.Combine(_runDirectory, "stereo-fixture.wav");
        var source = await WriteRealStereoFixtureAsync(wavPath);

        var separator = new SpectrogramOnnxStemSeparator(
            manager, neverCalledExpander, new WavStereoAudioDecoder(),
            outputDirectory: Path.Combine(_runDirectory, "out"));

        // SpectrogramOnnxStemSeparator always calls EnsureVerifiedAsync with userConsent:false (a
        // disclosed Step 044C decision: a real end-user consents once, up front, through the existing
        // Settings UI/ModelAssetManager flow -- never silently re-prompted mid-separation). A corrupted
        // on-disk file is detected, deleted, and -- with no consent already granted to re-download --
        // rejected via the existing, unmodified ModelAssetConsentRequiredException, never silently
        // extracted or passed to ONNX Runtime.
        await Assert.ThrowsAsync<ModelAssetConsentRequiredException>(
            () => separator.SeparateAsync(source, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe, progress: null, CancellationToken.None));

        Assert.False(File.Exists(verifiedPath), "the corrupted cached file must be deleted, never silently trusted or extracted");
        Assert.False(Directory.Exists(Path.Combine(_runDirectory, "out", ".extracted")), "extraction must never run against an unverified asset");
    }

    [Fact]
    public async Task SeparateAsync_AlreadyCancelledToken_ThrowsBeforeAnyPartialOutput_NegativePath()
    {
        var neverCalledDownloader = FakeModelAssetDownloader.ReturningBytes([]);
        var manager = new ModelAssetManager(_runDirectory, neverCalledDownloader);
        var neverCalledExpander = new NeverCalledArchiveExpander();
        var wavPath = Path.Combine(_runDirectory, "stereo-fixture.wav");
        var source = await WriteRealStereoFixtureAsync(wavPath);

        var outputDirectory = Path.Combine(_runDirectory, "cancelled-run");
        var separator = new SpectrogramOnnxStemSeparator(
            manager, neverCalledExpander, new WavStereoAudioDecoder(), outputDirectory);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => separator.SeparateAsync(source, SherpaOnnxSpleeterModelAsset.Bundle, SherpaOnnxSpleeterModelAsset.Recipe, progress: null, cts.Token));

        Assert.False(Directory.Exists(outputDirectory), "an already-cancelled run must leave no partial stem output on disk");
        Assert.Equal(0, neverCalledDownloader.CallCount);
    }
}
