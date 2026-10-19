using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 031's own persisted, versioned output: the raw RMS/energy feature envelope a later step's
/// onset/tempo analysis (Steps 032-034) will consume, kept strictly separate from any user-approved
/// <c>BeatGrid</c> (Section 2: "Separate raw audio features from human-approved beat grid"; Section 4:
/// "Store raw onset feature cache separately from corrected beat grids"). This envelope is pure
/// algorithm output — rebuildable at any time from <see cref="SourceHash"/>/<see cref="Settings"/>,
/// never itself a place user corrections are recorded.
/// </summary>
/// <param name="VersionId">Identity of this particular computed run — changes every time the envelope is rebuilt, even if the resulting frames are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this envelope was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> at analysis time — the cache-key content-identity half: a changed song (re-probed from different/edited bytes) never silently serves a stale envelope built from the old content.</param>
/// <param name="SourceDecoderVersion">The <c>SongAsset.DecoderVersion</c> at analysis time — a decoder upgrade invalidates previously computed envelopes rather than silently mixing old/new decode behavior.</param>
/// <param name="AnalyzerVersion">Identifies which analyzer (and its version) produced this envelope — mirrors <see cref="SourceDecoderVersion"/>'s own cache-key role, applied to the energy-extraction algorithm itself.</param>
/// <param name="Settings">The exact <see cref="RhythmSettings"/> this envelope's windows were computed with — part of the cache key per Section 4's "every cache key includes algorithm/model/settings versions."</param>
/// <param name="Frames">Contiguous, gap-free, half-open frames covering <c>[0, SampleCount)</c> of the source song at the configured hop, ordered by <see cref="EnergyFrame.StartSample"/>.</param>
public sealed record EnergyEnvelope(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    string SourceDecoderVersion,
    string AnalyzerVersion,
    RhythmSettings Settings,
    EnergyFrame[] Frames);
