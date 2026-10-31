using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// <see cref="TranscodingAudioDecoder"/> extends plain-WAV probing to non-WAV containers by
/// transcoding via ffmpeg. These tests exercise its orchestration (content-sniffing dispatch, the
/// ffmpeg-not-installed failure path, transcode-result caching) against fakes for
/// <see cref="IFfmpegInstaller"/>/<see cref="IExternalAudioTranscoder"/> — no real OS process is
/// launched here (see <see cref="FfmpegProcessTranscoder"/>'s own doc comment: that real process-launch
/// implementation is verified manually, not by an automated test that would otherwise need a real
/// ffmpeg binary in CI).
/// </summary>
public sealed class TranscodingAudioDecoderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-transcoding-decoder-{Guid.NewGuid():N}");

    public TranscodingAudioDecoderTests()
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

    private static byte[] BuildWavFixture() =>
        DeterministicSyntheticAudioGenerator.EncodeWav(
            DeterministicSyntheticAudioGenerator.BuildClickTrack(120, 4, 0.02, 1000.0, 8000), 8000);

    private sealed class FakeFfmpegInstaller : IFfmpegInstaller
    {
        private readonly string? _installedPath;

        private FakeFfmpegInstaller(string? installedPath) => _installedPath = installedPath;

        public static FakeFfmpegInstaller NotInstalled() => new(null);

        public static FakeFfmpegInstaller InstalledAt(string path) => new(path);

        public string? TryGetInstalledExecutablePath() => _installedPath;

        public Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct) =>
            throw new InvalidOperationException("TranscodingAudioDecoder must never download ffmpeg itself.");
    }

    private sealed class FakeExternalAudioTranscoder : IExternalAudioTranscoder
    {
        private readonly byte[]? _resultWavBytes;
        private readonly Exception? _failure;
        public int CallCount { get; private set; }

        public string OutputFormatVersion { get; init; } = "fake-format-v1";

        private FakeExternalAudioTranscoder(byte[]? resultWavBytes, Exception? failure)
        {
            _resultWavBytes = resultWavBytes;
            _failure = failure;
        }

        public static FakeExternalAudioTranscoder ProducingWav(byte[] wavBytes, string outputFormatVersion = "fake-format-v1") =>
            new(wavBytes, null) { OutputFormatVersion = outputFormatVersion };

        public static FakeExternalAudioTranscoder NeverCalled() => new(null,
            new InvalidOperationException("Transcoder must not be invoked when a cached result already exists."));

        public async Task TranscodeToWavAsync(string ffmpegExecutablePath, string sourcePath, string destinationPath, CancellationToken ct)
        {
            CallCount++;
            if (_failure is not null)
            {
                throw _failure;
            }

            await File.WriteAllBytesAsync(destinationPath, _resultWavBytes!, ct);
        }
    }

    [Fact]
    public async Task ProbeAsync_RealWavContent_DelegatesDirectlyAndNeverTouchesFfmpeg()
    {
        var wavBytes = BuildWavFixture();
        // Deliberately a ".mp3" extension on genuinely WAV bytes: dispatch must be content-based, never
        // extension-based (IAudioDecoder's own contract).
        var path = Path.Combine(_tempDir, "misnamed.mp3");
        await File.WriteAllBytesAsync(path, wavBytes);

        var cacheDir = Path.Combine(_tempDir, "cache");
        var decoder = new TranscodingAudioDecoder(
            new WavAudioDecoder(), FakeFfmpegInstaller.NotInstalled(), cacheDir, FakeExternalAudioTranscoder.NeverCalled());

        var asset = await decoder.ProbeAsync(path, CancellationToken.None);

        Assert.Equal(path, asset.FilePath);
        Assert.Equal(WavAudioDecoder.Version, asset.DecoderVersion);
        Assert.False(Directory.Exists(cacheDir));
    }

    [Fact]
    public async Task ProbeAsync_NonWavContent_NoFfmpegInstalled_ThrowsFfmpegNotAvailable()
    {
        var path = Path.Combine(_tempDir, "song.mp3");
        await File.WriteAllBytesAsync(path, "not a wav file at all"u8.ToArray());

        var cacheDir = Path.Combine(_tempDir, "cache");
        var decoder = new TranscodingAudioDecoder(
            new WavAudioDecoder(), FakeFfmpegInstaller.NotInstalled(), cacheDir, FakeExternalAudioTranscoder.NeverCalled());

        var ex = await Assert.ThrowsAsync<FfmpegNotAvailableException>(
            () => decoder.ProbeAsync(path, CancellationToken.None));

        Assert.Equal(path, ex.Path);
        Assert.False(Directory.Exists(cacheDir));
    }

    [Fact]
    public async Task ProbeAsync_NonWavContent_FfmpegInstalled_TranscodesCachesAndReusesCache()
    {
        var path = Path.Combine(_tempDir, "song.mp3");
        await File.WriteAllBytesAsync(path, "not a wav file at all"u8.ToArray());
        var wavBytes = BuildWavFixture();

        var cacheDir = Path.Combine(_tempDir, "cache");
        var ffmpegInstaller = FakeFfmpegInstaller.InstalledAt(Path.Combine(_tempDir, "fake-ffmpeg.exe"));
        var transcoder = FakeExternalAudioTranscoder.ProducingWav(wavBytes);
        var decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), ffmpegInstaller, cacheDir, transcoder);

        var asset = await decoder.ProbeAsync(path, CancellationToken.None);

        Assert.Equal(1, transcoder.CallCount);
        Assert.NotEqual(path, asset.FilePath);
        Assert.StartsWith(cacheDir, asset.FilePath);
        Assert.Equal($"{WavAudioDecoder.Version}+ffmpeg/{transcoder.OutputFormatVersion}", asset.DecoderVersion);
        Assert.True(File.Exists(asset.FilePath));

        // Re-probing the same source must reuse the cached transcode -- no second ffmpeg invocation.
        var secondAsset = await decoder.ProbeAsync(path, CancellationToken.None);
        Assert.Equal(1, transcoder.CallCount);
        Assert.Equal(asset.FilePath, secondAsset.FilePath);
    }

    [Fact]
    public async Task ProbeAsync_ChangedTranscoderOutputFormat_DoesNotReuseThePreviousCachedTranscode()
    {
        // Regression guard: the transcode cache key was originally the source content hash alone, so a
        // changed output setting (sample rate/channel count/codec) would silently resolve to a file
        // produced under the OLD settings -- against this repository's own "every cache key includes
        // algorithm/model/settings versions" rule. The transcoder's own OutputFormatVersion is now part
        // of the key, so the two formats must land on two distinct cache entries and each must really
        // run its own transcode.
        var path = Path.Combine(_tempDir, "song.mp3");
        await File.WriteAllBytesAsync(path, "not a wav file at all"u8.ToArray());
        var wavBytes = BuildWavFixture();
        var cacheDir = Path.Combine(_tempDir, "cache");
        var ffmpegInstaller = FakeFfmpegInstaller.InstalledAt(Path.Combine(_tempDir, "fake-ffmpeg.exe"));

        var firstFormat = FakeExternalAudioTranscoder.ProducingWav(wavBytes, "pcm_s16le-48000hz-1ch-v1");
        var firstAsset = await new TranscodingAudioDecoder(new WavAudioDecoder(), ffmpegInstaller, cacheDir, firstFormat)
            .ProbeAsync(path, CancellationToken.None);

        var secondFormat = FakeExternalAudioTranscoder.ProducingWav(wavBytes, "pcm_s16le-44100hz-2ch-v1");
        var secondAsset = await new TranscodingAudioDecoder(new WavAudioDecoder(), ffmpegInstaller, cacheDir, secondFormat)
            .ProbeAsync(path, CancellationToken.None);

        Assert.Equal(1, firstFormat.CallCount);
        Assert.Equal(1, secondFormat.CallCount); // really re-transcoded, never served the first entry.
        Assert.NotEqual(firstAsset.FilePath, secondAsset.FilePath);
        Assert.NotEqual(firstAsset.DecoderVersion, secondAsset.DecoderVersion);
        Assert.True(File.Exists(firstAsset.FilePath));
        Assert.True(File.Exists(secondAsset.FilePath));
    }

    [Fact]
    public async Task ProbeAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var decoder = new TranscodingAudioDecoder(
            new WavAudioDecoder(), FakeFfmpegInstaller.NotInstalled(), Path.Combine(_tempDir, "cache"), FakeExternalAudioTranscoder.NeverCalled());

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => decoder.ProbeAsync(Path.Combine(_tempDir, "does-not-exist.mp3"), CancellationToken.None));
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new TranscodingAudioDecoder(null!, FakeFfmpegInstaller.NotInstalled(), _tempDir));
        Assert.Throws<ArgumentNullException>(() => new TranscodingAudioDecoder(new WavAudioDecoder(), null!, _tempDir));
        Assert.Throws<ArgumentException>(() => new TranscodingAudioDecoder(new WavAudioDecoder(), FakeFfmpegInstaller.NotInstalled(), ""));
    }
}
