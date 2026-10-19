using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Shell.Tests;

/// <summary>
/// PHASE-06 Step 043 — "Detect instrument activity segments with scores." Fast, deterministic,
/// hardware-free unit tests for <see cref="StemMixerViewModel.BuildActivityAsync"/> -- the
/// SCREEN 08 STEMS application-layer slice that surfaces <see cref="StemActivityDetector"/>'s real,
/// non-mocked detection result per <see cref="StemMixerViewModel.StemChannelViewModel"/> as an
/// observable result in the UI layer, mirroring Step 042's own
/// <c>BuildWaveformsAsync_PopulatesPerStemCachePathsWithOriginPreserved</c> convention. No audio device
/// is needed for this step's own scope (detection only decodes files; it never opens a transport).
/// </summary>
public sealed class Step043StemMixerViewModelTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"xlai-step043-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private const int SampleRate = 8000;
    private const int WindowSamples = 80;

    private static float[] QuietThenLoud(int quietFrames, int loudFrames) =>
        new float[quietFrames * WindowSamples]
            .Concat(Enumerable.Range(0, loudFrames * WindowSamples)
                .Select(i => 0.9f * (float)Math.Sin(2 * Math.PI * 220.0 * i / SampleRate)))
            .ToArray();

    private async Task<(StemManifest Manifest, StemAsset Drums, StemAsset Vocals)> BuildManifestAsync()
    {
        var audioDir = Path.Combine(_rootDirectory, "stems");
        Directory.CreateDirectory(audioDir);

        var drumsPath = Path.Combine(audioDir, "drums.wav");
        var vocalsPath = Path.Combine(audioDir, "vocals.wav");
        var drumsSamples = QuietThenLoud(5, 10);
        var vocalsSamples = new float[drumsSamples.Length]; // fully silent -- no activity expected.
        await StemWavWriter.WriteMonoFloatAsync(drumsPath, drumsSamples, SampleRate, CancellationToken.None);
        await StemWavWriter.WriteMonoFloatAsync(vocalsPath, vocalsSamples, SampleRate, CancellationToken.None);

        var drumsProbe = await new WavAudioDecoder().ProbeAsync(drumsPath, CancellationToken.None);
        var vocalsProbe = await new WavAudioDecoder().ProbeAsync(vocalsPath, CancellationToken.None);

        var drums = new StemAsset(Guid.NewGuid(), StemKind.Drums, drumsPath, SampleRate, drumsSamples.Length, SourceOriginTicks: 0, drumsProbe.Sha256);
        var vocals = new StemAsset(Guid.NewGuid(), StemKind.Vocals, vocalsPath, SampleRate, vocalsSamples.Length, SourceOriginTicks: 0, vocalsProbe.Sha256);
        var manifest = new StemManifest(Guid.NewGuid(), "source-hash", "fixture-model", new[] { drums, vocals }, Array.Empty<string>());

        return (manifest, drums, vocals);
    }

    [Fact]
    public async Task BuildActivityAsync_PopulatesPerStemActivityRegions()
    {
        var (manifest, drums, vocals) = await BuildManifestAsync();
        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var detector = new StemActivityDetector(
            new WavAudioDecoder(),
            new StemActivityDetectionSettings(WindowSamples, WindowSamples, OnThreshold: 0.5, OffThreshold: 0.2, MinActiveDurationTicks: 0));

        var ok = await vm.BuildActivityAsync(detector);

        Assert.True(ok);
        Assert.Null(vm.ActivityErrorMessage);

        var drumsChannel = vm.Channels.Single(c => c.StemId == drums.Id);
        var vocalsChannel = vm.Channels.Single(c => c.StemId == vocals.Id);

        Assert.Single(drumsChannel.Activities);
        Assert.Equal(StemKind.Drums, drumsChannel.Activities[0].Kind);
        Assert.Empty(vocalsChannel.Activities); // fully silent stem -- no activity, not an error.
    }

    [Fact]
    public async Task BuildActivityAsync_NoManifestLoaded_SetsErrorMessage_NegativePath()
    {
        var vm = new StemMixerViewModel();
        var detector = new StemActivityDetector(
            new WavAudioDecoder(),
            new StemActivityDetectionSettings(WindowSamples, WindowSamples, OnThreshold: 0.5, OffThreshold: 0.2, MinActiveDurationTicks: 0));

        var ok = await vm.BuildActivityAsync(detector);

        Assert.False(ok);
        Assert.NotNull(vm.ActivityErrorMessage);
    }

    [Fact]
    public async Task BuildActivityAsync_StaleStemFile_SetsErrorMessage_NegativePath()
    {
        var (manifest, drums, _) = await BuildManifestAsync();

        // Overwrite the real file content without updating the declared manifest hash -- a stale/corrupt
        // stem, exactly like Step 042's own ApplyMixAndReopenAsync_IncompatibleStems negative path.
        await StemWavWriter.WriteMonoFloatAsync(drums.FilePath, new float[10], SampleRate, CancellationToken.None);

        var vm = new StemMixerViewModel();
        vm.LoadManifest(manifest);

        var detector = new StemActivityDetector(
            new WavAudioDecoder(),
            new StemActivityDetectionSettings(WindowSamples, WindowSamples, OnThreshold: 0.5, OffThreshold: 0.2, MinActiveDurationTicks: 0));

        var ok = await vm.BuildActivityAsync(detector);

        Assert.False(ok);
        Assert.NotNull(vm.ActivityErrorMessage);
    }
}
