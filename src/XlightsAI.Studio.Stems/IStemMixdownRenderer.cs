using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-06 Step 042 ("Add waveform tracks, solo/mute and synchronized stem playback"): renders the
/// currently-audible subset of a <see cref="StemManifest"/> (per <see cref="StemMixRules"/>) down to a
/// single mono mix, so the UI can hand exactly one <see cref="SongAsset"/> to the already-existing
/// single <see cref="IAudioTransport"/> -- Section 3's own architectural note, verbatim: "Solo/mute UI
/// is built on existing shared audio transport, not six independent drifting clocks."
/// </summary>
public interface IStemMixdownRenderer
{
    /// <exception cref="StemMixdownIncompatibleStemsException">The manifest's stems do not share a common sample rate/sample count/source origin, or a stem's file no longer matches its declared metadata.</exception>
    /// <exception cref="FileNotFoundException">An active stem's file no longer exists on disk.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<SongAsset> RenderMixAsync(StemManifest manifest, IReadOnlyList<StemChannelState> channels, CancellationToken ct);
}
