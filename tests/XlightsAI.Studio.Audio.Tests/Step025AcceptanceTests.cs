using System.Runtime.Versioning;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// PHASE-04 / Step 025 — "Add audio playback abstraction and actual desktop audio backend."
/// Required acceptance: "Play/pause/seek works without Python; missing output device handled."
/// Failure/edge case: "Silence, invalid timing boundaries and missing media must be handled
/// explicitly." Runs against the real Windows <c>winmm</c> device on this machine (confirmed
/// present via <c>Get-CimInstance Win32_SoundDevice</c> before this step began) — no mock audio
/// backend; real device output, real device-reported position.
/// </summary>
[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class Step025AcceptanceTests : IAsyncLifetime
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step025-{Guid.NewGuid():N}");
    private WaveOutAudioTransport? _transport;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDir);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
        }

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private async Task<SongAsset> ProbeClickTrackAsync(int clickCount = 6, int bpm = 240, int sampleRate = 8000)
    {
        var samples = DeterministicSyntheticAudioGenerator.BuildClickTrack(
            bpm, clickCount, clickDurationSeconds: 0.02, toneHz: 1000.0, sampleRate);
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(samples, sampleRate);
        var path = Path.Combine(_tempDir, $"click-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, wavBytes);
        return await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);
    }

    [RequiresAudioDeviceFact]
    public async Task Step025_Acceptance_Passes_PlayPauseSeekWorkOnRealAudioDevice()
    {
        // 240 BPM / 6 clicks => 1.5s real-time track; short enough to keep the test fast but long
        // enough to observe real, device-reported position advancing and pausing.
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());

        await _transport.OpenAsync(song, CancellationToken.None);
        Assert.Equal(song, _transport.CurrentSong);
        Assert.False(_transport.IsPlaying);
        Assert.Equal(0, _transport.PositionTicks);

        // Play: real device output; position must actually advance (public output from the real
        // backend, not a mock invocation count).
        await _transport.PlayAsync(CancellationToken.None);
        Assert.True(_transport.IsPlaying);

        var advanced = await WaitUntilAsync(() => _transport.PositionTicks > 0, TimeSpan.FromSeconds(2));
        Assert.True(advanced, "Real device position never advanced past 0 after Play.");

        // Pause: position must stop advancing.
        await _transport.PauseAsync(CancellationToken.None);
        Assert.False(_transport.IsPlaying);
        var pausedPosition = _transport.PositionTicks;
        await Task.Delay(150);
        Assert.Equal(pausedPosition, _transport.PositionTicks);

        // Seek while paused: position updates immediately without resuming playback.
        var seekTicks = song.DurationTicks / 2;
        await _transport.SeekAsync(seekTicks, CancellationToken.None);
        Assert.False(_transport.IsPlaying);
        Assert.Equal(seekTicks, _transport.PositionTicks);

        // Seek then Play: resumes from the sought position, continuing to advance.
        await _transport.PlayAsync(CancellationToken.None);
        Assert.True(_transport.IsPlaying);
        var advancedFromSeek = await WaitUntilAsync(() => _transport.PositionTicks > seekTicks, TimeSpan.FromSeconds(2));
        Assert.True(advancedFromSeek, "Position never advanced past the sought position after resuming Play.");

        await _transport.PauseAsync(CancellationToken.None);
    }

    [WindowsOnlyFact]
    public async Task OpenAsync_InvalidDeviceId_ThrowsAudioOutputDeviceUnavailableException_NegativePath()
    {
        // "missing output device handled" — deterministically exercised via an out-of-range device
        // ID rather than requiring literal hardware removal, which is not automatable.
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder(), deviceId: 99_999);

        var ex = await Assert.ThrowsAsync<AudioOutputDeviceUnavailableException>(
            () => _transport.OpenAsync(song, CancellationToken.None));

        Assert.True(ex.MmResultCode != 0);
    }

    [Fact]
    public async Task PlayAsync_WithoutOpen_ThrowsInvalidOperationException_NegativePath()
    {
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transport.PlayAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SeekAsync_WithoutOpen_ThrowsInvalidOperationException_NegativePath()
    {
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _transport.SeekAsync(0, CancellationToken.None));
    }

    [RequiresAudioDeviceFact]
    public async Task SeekAsync_NegativeTicks_ThrowsArgumentOutOfRangeException_NegativePath()
    {
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        await _transport.OpenAsync(song, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _transport.SeekAsync(-1, CancellationToken.None));
    }

    [RequiresAudioDeviceFact]
    public async Task SeekAsync_BeyondDuration_ThrowsArgumentOutOfRangeException_NegativePath()
    {
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        await _transport.OpenAsync(song, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _transport.SeekAsync(song.DurationTicks + 1, CancellationToken.None));
    }

    [RequiresAudioDeviceFact]
    public async Task SetLoop_InvalidWindow_ThrowsArgumentOutOfRangeException_NegativePath()
    {
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        await _transport.OpenAsync(song, CancellationToken.None);

        // Inverted window.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _transport!.SetLoop(new TimeWindow(1000, 500)));

        // Exceeds the open song's own duration.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _transport!.SetLoop(new TimeWindow(0, song.DurationTicks + 10_000_000)));
    }

    [RequiresAudioDeviceFact]
    public async Task SetLoop_ValidWindow_WrapsPlaybackWithoutExceedingLoopEnd()
    {
        var song = await ProbeClickTrackAsync(clickCount: 8, bpm: 240, sampleRate: 8000); // 2s track
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        await _transport.OpenAsync(song, CancellationToken.None);

        // Loop the first quarter-second only.
        var loopEndTicks = song.DurationTicks / 8;
        _transport.SetLoop(new TimeWindow(0, loopEndTicks));

        await _transport.PlayAsync(CancellationToken.None);

        // Wait well past one full loop duration; real position must have wrapped back rather than
        // continuing straight through to (or past) the loop end.
        await Task.Delay((int)(loopEndTicks / 10_000) * 3 + 150);

        // Sample the real device position several times; every sample must still be within a small
        // tolerance of the loop window, proving it wrapped at least once rather than playing through.
        for (var i = 0; i < 3; i++)
        {
            var position = _transport.PositionTicks;
            Assert.True(position <= loopEndTicks + 1_000_000, // +100ms tolerance for poll granularity
                $"Position {position} exceeded the loop end {loopEndTicks} by more than tolerance; loop did not wrap.");
            await Task.Delay(40);
        }

        await _transport.PauseAsync(CancellationToken.None);
    }

    [RequiresAudioDeviceFact]
    public async Task OpenAsync_SilentAudio_PlaysAndAdvancesWithoutThrowing_HandlesSilenceExplicitly()
    {
        var silentSamples = new float[8000]; // 1 second of true digital silence at 8 kHz.
        var wavBytes = DeterministicSyntheticAudioGenerator.EncodeWav(silentSamples, sampleRate: 8000);
        var path = Path.Combine(_tempDir, "silence.wav");
        await File.WriteAllBytesAsync(path, wavBytes);
        var song = await new WavAudioDecoder().ProbeAsync(path, CancellationToken.None);

        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        await _transport.OpenAsync(song, CancellationToken.None);
        await _transport.PlayAsync(CancellationToken.None);

        var advanced = await WaitUntilAsync(() => _transport.PositionTicks > 0, TimeSpan.FromSeconds(2));
        Assert.True(advanced, "Silent audio must still advance real device position, not be skipped as 'no signal'.");

        await _transport.PauseAsync(CancellationToken.None);
    }

    [Fact]
    public async Task OpenAsync_MissingMediaFile_PropagatesFileNotFoundException_NegativePath()
    {
        // A SongAsset whose FilePath no longer exists (the "audio file moved after import" disclosed
        // Section 7 risk) must fail clearly when (re-)decoding for playback, not silently play nothing.
        var song = await ProbeClickTrackAsync();
        var moved = song with { FilePath = Path.Combine(_tempDir, "does-not-exist-anymore.wav") };

        _transport = new WaveOutAudioTransport(new WavAudioDecoder());

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _transport.OpenAsync(moved, CancellationToken.None));
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }
}
