using System.Runtime.Versioning;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Reference.Fixtures;

namespace XlightsAI.Studio.Audio.Tests;

/// <summary>
/// PHASE-04 / Step 028 — "Use one master media time clock for playback and playhead." Required
/// acceptance: "Repeated seeks do not accumulate drift." Implementation guidance: "Expose one
/// authoritative playhead and scrub preview; verify time conversion uses song-origin and
/// sample-rate not UI frame interval." Failure/edge case to prove (this step's own interpretation
/// of the spec's templated "Out-of-scope, locked or stale revision must be rejected without
/// partial mutation" text — disclosed in <c>implementation-evidence/PHASE-04/STEP-028.md</c> §3,
/// since this step's own scope has no document/edit-transaction revision to check): an out-of-scope
/// seek target and a "stale" (no song open) master-clock seek must both be rejected before any
/// transport call, leaving <see cref="MasterPlaybackClock.CanonicalPositionTicks"/> exactly as it
/// was. Runs against the real Windows <c>winmm</c> device on this machine — no mock
/// <see cref="IAudioTransport"/>, same convention as every Step 025/026/027 test.
/// </summary>
[Trait("Category", "Integration")]
[SupportedOSPlatform("windows")]
public sealed class Step028AcceptanceTests : IAsyncLifetime
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"xlai-step028-{Guid.NewGuid():N}");
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

    [Fact]
    public async Task Step028_Acceptance_Passes_RepeatedSeeksToSameTargetDoNotAccumulateDrift()
    {
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var clock = new MasterPlaybackClock(_transport);

        await _transport.OpenAsync(song, CancellationToken.None);

        var target = song.DurationTicks / 3;

        // Seek to the same canonical target 20 times in a row (paused, so no real-time playback
        // advance can occur between seeks) — the real device position must be bit-identical every
        // time, proving the result is a pure function of the requested tick, not an incremental
        // adjustment from whatever the previous position happened to be.
        long? first = null;
        for (var i = 0; i < 20; i++)
        {
            await clock.SeekToCanonicalAsync(target, CancellationToken.None);
            var observed = clock.CanonicalPositionTicks;
            first ??= observed;
            Assert.Equal(first.Value, observed);
        }
    }

    [Fact]
    public async Task Step028_Acceptance_Passes_AlternatingSeeksReturnExactlyToTheSameTargetEachTime()
    {
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var clock = new MasterPlaybackClock(_transport);

        await _transport.OpenAsync(song, CancellationToken.None);

        var targetA = song.DurationTicks / 4;
        var targetB = (song.DurationTicks * 3) / 4;

        await clock.SeekToCanonicalAsync(targetA, CancellationToken.None);
        var firstA = clock.CanonicalPositionTicks;

        for (var i = 0; i < 10; i++)
        {
            await clock.SeekToCanonicalAsync(targetB, CancellationToken.None);
            await clock.SeekToCanonicalAsync(targetA, CancellationToken.None);
            Assert.Equal(firstA, clock.CanonicalPositionTicks);
        }
    }

    [Fact]
    public async Task CanonicalPositionTicks_IncludesSongOriginTicks_NotJustRawTransportPosition()
    {
        var song = await ProbeClickTrackAsync();
        // A non-zero OriginTicks (a later phase's "re-origin a song against a shared project
        // timeline" case, per SongAsset's own doc comment) — proves the conversion genuinely adds
        // the song's origin rather than only ever passing the raw transport position through.
        var reOriginedSong = song with { OriginTicks = 5_000_000 };

        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var clock = new MasterPlaybackClock(_transport);
        await _transport.OpenAsync(reOriginedSong, CancellationToken.None);

        var relativeTarget = song.DurationTicks / 2;
        await clock.SeekToCanonicalAsync(reOriginedSong.OriginTicks + relativeTarget, CancellationToken.None);

        Assert.Equal(relativeTarget, _transport.PositionTicks);
        Assert.Equal(reOriginedSong.OriginTicks + relativeTarget, clock.CanonicalPositionTicks);
    }

    [Fact]
    public async Task SeekToCanonicalAsync_OutOfScopeTarget_ThrowsWithoutPartialMutation_NegativePath()
    {
        var song = await ProbeClickTrackAsync();
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var clock = new MasterPlaybackClock(_transport);
        await _transport.OpenAsync(song, CancellationToken.None);

        var knownGoodTarget = song.DurationTicks / 2;
        await clock.SeekToCanonicalAsync(knownGoodTarget, CancellationToken.None);
        var positionBefore = clock.CanonicalPositionTicks;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => clock.SeekToCanonicalAsync(song.DurationTicks + 10_000_000, CancellationToken.None));

        // No partial mutation: the real transport position is completely unchanged by the rejected
        // out-of-scope request.
        Assert.Equal(positionBefore, clock.CanonicalPositionTicks);
    }

    [Fact]
    public async Task SeekToCanonicalAsync_NoSongOpen_ThrowsInvalidOperationException_StaleSessionNegativePath()
    {
        _transport = new WaveOutAudioTransport(new WavAudioDecoder());
        var clock = new MasterPlaybackClock(_transport);

        Assert.False(clock.IsOpen);
        Assert.Equal(0, clock.CanonicalPositionTicks);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => clock.SeekToCanonicalAsync(0, CancellationToken.None));

        // Still exactly the pre-call neutral state — no partial mutation from a stale/missing session.
        Assert.False(clock.IsOpen);
        Assert.Equal(0, clock.CanonicalPositionTicks);
    }
}
