using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// Covers the Mac/Linux placeholder backend (<c>docs/ARCHITECTURE-AND-GUARDRAILS.md</c>'s "Platform
/// coverage" rule): every platform without a real <see cref="IAudioTransport"/> implementation must
/// get a stub that throws one clear, typed message rather than silently no-op-ing or surfacing a raw
/// native-interop failure. This class is not itself platform-gated, so it runs identically on every
/// OS this test suite happens to execute on.
/// </summary>
public sealed class UnsupportedPlatformAudioTransportTests
{
    private static SongAsset MakeSongAsset() =>
        new(
            Id: new SongId(Guid.NewGuid()),
            FilePath: "irrelevant.wav",
            Sha256: new string('0', 64),
            SampleCount: 8000,
            SampleRate: 8000,
            Channels: 1,
            OriginTicks: 0,
            BitsPerSample: 16,
            EncoderDelaySamples: 0,
            DecoderVersion: "test");

    [Fact]
    public async Task OpenAsync_Throws_ClearUnsupportedAudioPlatformException()
    {
        var transport = new UnsupportedPlatformAudioTransport();

        var ex = await Assert.ThrowsAsync<UnsupportedAudioPlatformException>(
            () => transport.OpenAsync(MakeSongAsset(), CancellationToken.None));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        Assert.False(string.IsNullOrWhiteSpace(ex.PlatformDescription));
    }

    [Fact]
    public async Task PlayAsync_PauseAsync_SeekAsync_AllThrow_ClearUnsupportedAudioPlatformException()
    {
        var transport = new UnsupportedPlatformAudioTransport();

        await Assert.ThrowsAsync<UnsupportedAudioPlatformException>(() => transport.PlayAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnsupportedAudioPlatformException>(() => transport.PauseAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnsupportedAudioPlatformException>(() => transport.SeekAsync(0, CancellationToken.None));
    }

    [Fact]
    public void SetLoop_Throws_ClearUnsupportedAudioPlatformException()
    {
        var transport = new UnsupportedPlatformAudioTransport();

        Assert.Throws<UnsupportedAudioPlatformException>(() => transport.SetLoop(new TimeWindow(0, 1)));
    }

    [Fact]
    public void Properties_ReportSafeNeutralDefaults_WithoutThrowing()
    {
        var transport = new UnsupportedPlatformAudioTransport();

        Assert.Null(transport.CurrentSong);
        Assert.False(transport.IsPlaying);
        Assert.Null(transport.Loop);
        Assert.Equal(0, transport.PositionTicks);
    }

    [Fact]
    public async Task DisposeAsync_NeverThrows_EvenThoughNothingWasEverOpened()
    {
        var transport = new UnsupportedPlatformAudioTransport();

        await transport.DisposeAsync();
    }
}
