namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 028 ("Use one master media time clock for playback and playhead"): the single
/// authoritative conversion between the real device-reported <see cref="IAudioTransport.PositionTicks"/>
/// and the canonical, song-origin-relative timeline position every screen that shows a playhead
/// (the Audio screen's own position display, the Timeline's playhead/scrub preview) must agree on.
/// Per Section 3's architectural design notes ("The playback component owns one authoritative
/// monotonic transport clock; views subscribe to position snapshots, not their own timers") and this
/// step's own implementation guidance ("verify time conversion uses song-origin and sample-rate not
/// UI frame interval"), this wraps the already-real, sample-rate-accurate <see cref="IAudioTransport"/>
/// rather than introducing any UI-timer/frame-based estimate of its own — it performs exactly one
/// arithmetic operation (<see cref="SongAsset.OriginTicks"/> + the transport's own device-reported
/// ticks) and delegates every actual seek back to the transport, which itself is sample-rate-based
/// (see <c>WaveOutAudioTransport.TicksToSamples</c>/<c>SamplesToTicks</c>).
/// </summary>
public sealed class MasterPlaybackClock
{
    private readonly IAudioTransport _transport;

    public MasterPlaybackClock(IAudioTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary><see langword="true"/> exactly when a song is currently open on the wrapped transport.</summary>
    public bool IsOpen => _transport.CurrentSong is not null;

    /// <summary>
    /// The canonical, song-origin-relative playback position in 100 ns ticks:
    /// <c>CurrentSong.OriginTicks + transport.PositionTicks</c>. <c>0</c> while no song is open.
    /// </summary>
    public long CanonicalPositionTicks => _transport.CurrentSong is { } song
        ? song.OriginTicks + _transport.PositionTicks
        : 0;

    /// <summary>
    /// Seeks the wrapped transport so <see cref="CanonicalPositionTicks"/> becomes
    /// <paramref name="canonicalTicks"/> (converted back to the transport's own song-relative ticks
    /// by subtracting the open song's <see cref="SongAsset.OriginTicks"/>). Repeated calls with the
    /// same <paramref name="canonicalTicks"/> always resolve to the exact same
    /// <see cref="CanonicalPositionTicks"/> afterward — the real device position is a pure function
    /// of the requested tick, never an incremental adjustment from whatever the previous position
    /// happened to be — so seeks never accumulate drift.
    /// </summary>
    /// <exception cref="InvalidOperationException">No song is currently open — a stale/missing playback session; rejected before any transport call, so no partial mutation occurs.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="canonicalTicks"/> falls outside the open song's canonical timing boundary; rejected before any transport call.</exception>
    public Task SeekToCanonicalAsync(long canonicalTicks, CancellationToken ct)
    {
        if (_transport.CurrentSong is not { } song)
        {
            throw new InvalidOperationException(
                "Cannot seek the master playback clock: no song is currently open on the playback transport (stale/missing playback session).");
        }

        var relativeTicks = canonicalTicks - song.OriginTicks;
        if (relativeTicks < 0 || relativeTicks > song.DurationTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(canonicalTicks), canonicalTicks,
                $"Seek target must be within the open song's canonical timing boundary [{song.OriginTicks}, {song.OriginTicks + song.DurationTicks}].");
        }

        return _transport.SeekAsync(relativeTicks, ct);
    }
}
