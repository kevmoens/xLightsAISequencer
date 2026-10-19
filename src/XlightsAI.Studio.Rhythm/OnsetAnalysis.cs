using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 032's own persisted, versioned output: the raw onset/transient candidate list a later step's
/// tempo/beat-grid analysis (Steps 033-034) will consume, kept strictly separate from any
/// user-approved <c>BeatGrid</c> (Section 2: "Separate raw audio features from human-approved beat
/// grid"; Section 4: "Store raw onset feature cache separately from corrected beat grids"). Mirrors
/// <see cref="EnergyEnvelope"/>'s own provenance shape exactly, with <see cref="OnsetSettings"/> added
/// as this step's own additional cache-key component.
/// </summary>
/// <param name="VersionId">Identity of this particular computed run — changes every time the analysis is rebuilt, even if the resulting onsets are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> at analysis time.</param>
/// <param name="SourceDecoderVersion">The <c>SongAsset.DecoderVersion</c> at analysis time.</param>
/// <param name="AnalyzerVersion">Identifies which detector (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="RhythmSettings"/> windowing this run used.</param>
/// <param name="OnsetSettings">The exact <see cref="OnsetDetectionSettings"/> this run used — a second, independent cache-key component alongside <see cref="Settings"/>.</param>
/// <param name="Onsets">Detected onset candidates, ordered by <see cref="OnsetEvent.TimeTicks"/>.</param>
public sealed record OnsetAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    string SourceDecoderVersion,
    string AnalyzerVersion,
    RhythmSettings Settings,
    OnsetDetectionSettings OnsetSettings,
    OnsetEvent[] Onsets);
