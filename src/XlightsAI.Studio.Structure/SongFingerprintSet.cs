using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 052's own persisted, versioned output: every <see cref="BarFingerprint"/> computed for one song,
/// tied back to the exact source audio and the exact <c>BeatGrid</c> revision its bar windows were
/// derived from (Section 4: "every cache key includes algorithm/model/settings versions" -- applied here
/// across the full provenance chain, not just this step's own settings).
/// </summary>
/// <param name="VersionId">Identity of this particular computed run -- changes every time the set is rebuilt, even if the resulting fingerprints are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this set was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> both the consumed <c>BeatGrid</c> and <c>EnergyEnvelope</c> were themselves built from.</param>
/// <param name="SourceBeatGridVersionId">The exact <c>BeatGrid.VersionId</c> this set's bar windows were partitioned against -- a rebuilt (even numerically-identical) beat grid always invalidates this cache.</param>
/// <param name="AnalyzerVersion">Identifies which builder (and its version) produced this set.</param>
/// <param name="Settings">The exact <see cref="BarFingerprintSettings"/> this run used.</param>
/// <param name="Bars">Computed fingerprints, ordered by <see cref="BarFingerprint.BarIndex"/>. Empty, never an exception, when the source <c>BeatGrid.Beats</c> was itself empty (silence) -- this step's own "silence... must be handled explicitly" edge case.</param>
public sealed record SongFingerprintSet(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceBeatGridVersionId,
    string AnalyzerVersion,
    BarFingerprintSettings Settings,
    IReadOnlyList<BarFingerprint> Bars);
