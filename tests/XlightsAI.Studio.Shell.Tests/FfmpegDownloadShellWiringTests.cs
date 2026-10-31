using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// Proves <see cref="ShellViewModel.DownloadFfmpegCommand"/>/<see cref="AudioImportViewModel.RequiresFfmpegDownload"/>
/// are wired end to end: probing a non-WAV file through a real <see cref="TranscodingAudioDecoder"/>
/// surfaces the "download ffmpeg" affordance instead of a dead-end error, and a successful download
/// clears it so a subsequent probe succeeds.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FfmpegDownloadShellWiringTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xlai-ffmpeg-shell-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeFfmpegInstaller : IFfmpegInstaller
    {
        private readonly string _pathOnceInstalled;
        private bool _installed;

        public FakeFfmpegInstaller(string pathOnceInstalled) => _pathOnceInstalled = pathOnceInstalled;

        public string? TryGetInstalledExecutablePath() => _installed ? _pathOnceInstalled : null;

        public Task<string> EnsureInstalledAsync(IProgress<double>? progress, CancellationToken ct)
        {
            _installed = true;
            return Task.FromResult(_pathOnceInstalled);
        }
    }

    private sealed class FakeExternalAudioTranscoder : IExternalAudioTranscoder
    {
        private readonly byte[] _wavBytes;
        public FakeExternalAudioTranscoder(byte[] wavBytes) => _wavBytes = wavBytes;

        public string OutputFormatVersion => "fake-format-v1";

        public Task TranscodeToWavAsync(string ffmpegExecutablePath, string sourcePath, string destinationPath, CancellationToken ct) =>
            File.WriteAllBytesAsync(destinationPath, _wavBytes, ct);
    }

    [Fact]
    public async Task ProbeNonWavFile_OffersDownload_ThenSucceedsAfterDownload()
    {
        Directory.CreateDirectory(_root);
        var mp3Path = Path.Combine(_root, "song.mp3");
        await File.WriteAllBytesAsync(mp3Path, "definitely not a wav file"u8.ToArray());

        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(
            DeterministicSyntheticAudioGenerator.BuildClickTrack(120, 4, 0.02, 1000.0, 8000), 8000);

        var ffmpegInstaller = new FakeFfmpegInstaller(Path.Combine(_root, "fake-ffmpeg.exe"));
        var decoder = new TranscodingAudioDecoder(
            new WavAudioDecoder(), ffmpegInstaller, Path.Combine(_root, "cache"), new FakeExternalAudioTranscoder(wavBytes));

        var navigator = new ShellNavigator();
        var shell = new ShellViewModel(
            navigator,
            () => Array.Empty<string>(),
            audioDecoder: decoder,
            songAssetStore: new JsonSongAssetStore(Path.Combine(_root, "audio-assets")),
            ffmpegInstaller: ffmpegInstaller);
        shell.RunStartup();
        shell.NavigateToAudioCommand.Execute(null);

        shell.Audio.AudioFilePath = mp3Path;
        Assert.False(shell.DownloadFfmpegCommand.CanExecute(null));
        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.False(shell.Audio.HasProbedAsset);
        Assert.True(shell.Audio.RequiresFfmpegDownload);
        Assert.True(shell.DownloadFfmpegCommand.CanExecute(null));

        shell.DownloadFfmpegCommand.Execute(null);
        await shell.LastDownloadFfmpegCommandTask!;

        Assert.False(shell.Audio.RequiresFfmpegDownload);
        Assert.False(shell.DownloadFfmpegCommand.CanExecute(null));
        Assert.False(string.IsNullOrEmpty(shell.Audio.FfmpegStatusMessage));

        shell.ProbeAudioFileCommand.Execute(null);
        await shell.LastProbeAudioFileCommandTask!;

        Assert.True(shell.Audio.HasProbedAsset);
        Assert.False(shell.Audio.RequiresFfmpegDownload);
    }

    [Fact]
    public void ShellViewModel_WithoutFfmpegInstallerConfigured_StillConstructsAndStarts_BackwardCompatible()
    {
        var navigator = new ShellNavigator();
        var vm = new ShellViewModel(navigator, () => Array.Empty<string>());

        vm.RunStartup();
        vm.NavigateToAudioCommand.Execute(null);

        Assert.False(vm.DownloadFfmpegCommand.CanExecute(null));
    }
}
