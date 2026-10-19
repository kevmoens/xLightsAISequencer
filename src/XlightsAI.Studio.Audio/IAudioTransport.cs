using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// PHASE-04 Step 025 — Section 3's proposed playback boundary: one authoritative transport that
/// owns actual device output and the single monotonic playback clock views synchronize against
/// (Section 3 architectural design notes: "The playback component owns one authoritative monotonic
/// transport clock; views subscribe to position snapshots, not their own timers"). Extended beyond
/// Section 3's bare proposed shape with <see cref="IsPlaying"/>, <see cref="CurrentSong"/>,
/// <see cref="Loop"/> and <see cref="IAsyncDisposable"/> — disclosed in
/// <c>implementation-evidence/PHASE-04/STEP-25.md</c> §3, the same "first introduced by this step,
/// so its own additions are not a migration" reasoning Step 024 already applied to
/// <see cref="SongAsset"/>.
/// </summary>
public interface IAudioTransport : IAsyncDisposable
{
    /// <summary>
    /// Current playback position in canonical 100 ns ticks (Section 2), measured from the actual
    /// device-reported sample position while playing, or the last paused/seeked position while not
    /// — never a UI-timer estimate.
    /// </summary>
    long PositionTicks { get; }

    /// <summary><see langword="true"/> only while the device is actively advancing <see cref="PositionTicks"/>.</summary>
    bool IsPlaying { get; }

    /// <summary>The song most recently opened via <see cref="OpenAsync"/>; <see langword="null"/> until a song is open.</summary>
    SongAsset? CurrentSong { get; }

    /// <summary>The currently configured loop window, if any (<see cref="SetLoop"/>).</summary>
    TimeWindow? Loop { get; }

    /// <summary>
    /// Opens <paramref name="song"/> for playback, replacing any previously open song. Does not
    /// start playback; call <see cref="PlayAsync"/> afterward.
    /// </summary>
    /// <exception cref="AudioOutputDeviceUnavailableException">No usable audio output device is available.</exception>
    Task OpenAsync(SongAsset song, CancellationToken ct);

    /// <summary>Starts or resumes playback from the current position.</summary>
    /// <exception cref="InvalidOperationException">No song is open (<see cref="OpenAsync"/> has not succeeded).</exception>
    Task PlayAsync(CancellationToken ct);

    /// <summary>Pauses playback; <see cref="PositionTicks"/> stops advancing and does not reset.</summary>
    Task PauseAsync(CancellationToken ct);

    /// <summary>
    /// Moves the playback position to <paramref name="ticks"/> without changing whether playback is
    /// running — a seek while playing keeps playing from the new position; a seek while paused stays
    /// paused at the new position.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ticks"/> is outside <c>[0, CurrentSong.DurationTicks]</c> — an invalid timing boundary.</exception>
    /// <exception cref="InvalidOperationException">No song is open.</exception>
    Task SeekAsync(long ticks, CancellationToken ct);

    /// <summary>
    /// Sets (or, with <see langword="null"/>, clears) the loop window: while playing, reaching
    /// <see cref="TimeWindow.EndTicks"/> wraps back to <see cref="TimeWindow.StartTicks"/> without
    /// stopping playback.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The window is empty/inverted or exceeds the open song's duration.</exception>
    void SetLoop(TimeWindow? window);
}
