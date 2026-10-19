using System.Runtime.InteropServices;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// Placeholder <see cref="IAudioTransport"/> for any operating system that has no real playback
/// backend yet (every platform except Windows — see <see cref="WaveOutAudioTransport"/>). Every
/// action method throws a clear, typed <see cref="UnsupportedAudioPlatformException"/> naming the
/// gap; it never silently no-ops and never lets a raw native-interop failure (e.g. a "library not
/// found" exception from attempting <c>winmm.dll</c> on a platform that does not have it) surface
/// instead. Composition roots (<c>Program.cs</c>, <c>App.axaml.cs</c>) select this automatically via
/// an <see cref="OperatingSystem.IsWindows"/> guard — see
/// <c>docs/ARCHITECTURE-AND-GUARDRAILS.md</c>'s "Platform coverage" rule: any OS-specific concrete
/// implementation that has no cross-platform equivalent yet must ship a stub like this one for the
/// platforms it does not cover, rather than leave them unhandled.
/// </summary>
public sealed class UnsupportedPlatformAudioTransport : IAudioTransport
{
    public SongAsset? CurrentSong => null;

    public bool IsPlaying => false;

    public TimeWindow? Loop => null;

    public long PositionTicks => 0;

    public Task OpenAsync(SongAsset song, CancellationToken ct) => throw NotSupported();

    public Task PlayAsync(CancellationToken ct) => throw NotSupported();

    public Task PauseAsync(CancellationToken ct) => throw NotSupported();

    public Task SeekAsync(long ticks, CancellationToken ct) => throw NotSupported();

    public void SetLoop(TimeWindow? window) => throw NotSupported();

    /// <summary>A no-op, not a throw: disposing an object that was never successfully opened must always succeed.</summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static UnsupportedAudioPlatformException NotSupported() =>
        new(RuntimeInformation.OSDescription);
}
