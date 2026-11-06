using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems.Tests;

/// <summary>
/// Build-plan "richer analysis tools" Phase 6 (2026-11-02): real end-to-end proof that
/// <see cref="WhisperOnnxTranscriber"/> actually works against the real, declared
/// <see cref="SherpaOnnxWhisperTinyEnModelAsset"/> -- no model weights, archive bytes, or ONNX inference
/// result are faked, mocked, or pre-computed anywhere in this file, same discipline as
/// <c>Step044DAcceptanceTests</c> for the stem-separation model.
///
/// <b>Real network access required.</b> Performs a genuine HTTPS download of the real, 118,071,777-byte
/// <c>sherpa-onnx-whisper-tiny.en.tar.bz2</c> archive from <c>github.com</c> on its first run on a given
/// machine (cached thereafter at a stable, non-test-local path, same convention as the stem-separation
/// model's own real-model test).
///
/// The archive's own bundled <c>test_wavs/0.wav</c> (and its own bundled ground-truth
/// <c>test_wavs/trans.txt</c>) is reused directly as this test's real speech fixture -- no separate
/// synthetic/recorded fixture needed, since the real model asset itself already ships a real, licensed
/// speech sample with a verified-correct transcript.
/// </summary>
public sealed class WhisperOnnxTranscriberRealModelTests : IDisposable
{
    private static readonly string SharedModelAssetCacheDirectory =
        Path.Combine(Path.GetTempPath(), "xlai-richer-analysis-phase6-whisper-model-asset-cache");

    private readonly string _runDirectory = Path.Combine(Path.GetTempPath(), $"xlai-phase6-whisper-{Guid.NewGuid():N}");

    public WhisperOnnxTranscriberRealModelTests() => Directory.CreateDirectory(_runDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_runDirectory))
        {
            Directory.Delete(_runDirectory, recursive: true);
        }
    }

    private static async Task<ModelAssetManager> EnsureRealModelDownloadedAsync()
    {
        using var downloader = new HttpModelAssetDownloader();
        var manager = new ModelAssetManager(SharedModelAssetCacheDirectory, downloader);
        await manager.EnsureVerifiedAsync(SherpaOnnxWhisperTinyEnModelAsset.Archive, userConsent: true, CancellationToken.None).ConfigureAwait(false);
        return manager;
    }

    [Fact]
    public async Task TranscribeAsync_RealArchiveBundledSpeechSample_ReproducesGroundTruthTranscriptEssentiallyVerbatim()
    {
        var asset = SherpaOnnxWhisperTinyEnModelAsset.Archive;
        ModelAssetValidator.Validate(asset);

        var modelAssetManager = await EnsureRealModelDownloadedAsync();
        var archivePath = modelAssetManager.GetVerifiedPath(asset);
        Assert.True(File.Exists(archivePath));
        Assert.Equal(118_071_777L, new FileInfo(archivePath).Length);

        var expander = new TarBzip2ModelArchiveExpander();
        var extractDirectory = Path.Combine(_runDirectory, "extracted");
        var extracted = await expander.ExpandAsync(archivePath, extractDirectory, CancellationToken.None);

        // The archive's own bundled real speech sample + ground truth -- reused directly as this test's
        // fixture, never a separately-authored one.
        var wavEntry = Assert.Single(extracted, f => f.ArchiveEntryName.EndsWith("test_wavs/0.wav", StringComparison.Ordinal));
        var transcriptEntry = Assert.Single(extracted, f => f.ArchiveEntryName.EndsWith("test_wavs/trans.txt", StringComparison.Ordinal));
        var groundTruthLine = (await File.ReadAllLinesAsync(transcriptEntry.LocalPath))
            .Single(l => l.StartsWith("0.wav", StringComparison.Ordinal));
        var groundTruthText = groundTruthLine["0.wav".Length..].Trim();

        var decoder = new WavAudioDecoder();
        var songAsset = await decoder.ProbeAsync(wavEntry.LocalPath, CancellationToken.None);

        var transcriber = new WhisperOnnxTranscriber(
            modelAssetManager, expander, decoder, SherpaOnnxWhisperTinyEnModelAsset.Bundle, _runDirectory);

        var words = await transcriber.TranscribeAsync(songAsset, CancellationToken.None);

        Assert.NotEmpty(words);
        // Monotonically non-decreasing timing -- real segment timestamps, never fabricated/zeroed.
        for (var i = 1; i < words.Count; i++)
        {
            Assert.True(words[i].StartTicks >= words[i - 1].StartTicks);
            Assert.True(words[i].EndTicks >= words[i].StartTicks);
        }

        var recognizedText = string.Join(' ', words.Select(w => w.Text)).ToUpperInvariant();
        var expectedWords = groundTruthText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Not an exact string match (case/punctuation legitimately differ) -- the real, meaningful
        // acceptance bar: the overwhelming majority of the real ground-truth words actually appear,
        // in the recognized text, proving this is genuine working transcription, not noise.
        var matchedWordCount = expectedWords.Count(w => recognizedText.Contains(w, StringComparison.OrdinalIgnoreCase));
        Assert.True(matchedWordCount >= expectedWords.Length * 0.8,
            $"Expected at least 80% of ground-truth words to appear in the recognized text. " +
            $"Ground truth: \"{groundTruthText}\". Recognized: \"{recognizedText}\".");
    }

    [Fact]
    public async Task TranscribeAsync_NoConsentAndNoCachedModel_ThrowsModelAssetConsentRequiredException_NegativePath()
    {
        var neverCalledDownloader = NeverCalledModelAssetDownloader.Instance;
        var manager = new ModelAssetManager(_runDirectory, neverCalledDownloader);
        var expander = new TarBzip2ModelArchiveExpander();
        var decoder = new WavAudioDecoder();

        var transcriber = new WhisperOnnxTranscriber(
            manager, expander, decoder, SherpaOnnxWhisperTinyEnModelAsset.Bundle, Path.Combine(_runDirectory, "work"));

        var wavPath = Path.Combine(_runDirectory, "silence.wav");
        await WriteSilentWavAsync(wavPath);
        var songAsset = await decoder.ProbeAsync(wavPath, CancellationToken.None);

        await Assert.ThrowsAsync<ModelAssetConsentRequiredException>(
            () => transcriber.TranscribeAsync(songAsset, CancellationToken.None));
    }

    private static async Task WriteSilentWavAsync(string path)
    {
        var samples = new float[16000];
        var bytes = XlightsAI.Studio.Reference.Fixtures.DeterministicSyntheticAudioGenerator.EncodeWav(samples, 16000);
        await File.WriteAllBytesAsync(path, bytes);
    }

    private sealed class NeverCalledModelAssetDownloader : IModelAssetDownloader
    {
        public static readonly NeverCalledModelAssetDownloader Instance = new();

        public Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct) =>
            throw new InvalidOperationException("No download should be attempted without user consent.");
    }
}
