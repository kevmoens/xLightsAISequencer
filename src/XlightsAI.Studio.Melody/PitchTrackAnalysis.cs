namespace XlightsAI.Studio.Melody;

/// <summary>
/// Step 046's own persisted, versioned analysis artifact: a raw per-hop fundamental-frequency contour
/// for one isolated-lead <c>XlightsAI.Studio.Stems.StemAsset</c>, kept strictly separate from the
/// corrected, note-segmented <see cref="MelodyTrack"/> a later step (048-049) will derive from it --
/// mirroring <c>XlightsAI.Studio.Rhythm.OnsetAnalysis</c>'s own "raw feature cache, not the
/// human-approved result" shape exactly.
/// </summary>
/// <param name="VersionId">Identity of this particular computed run -- changes every time the analysis is rebuilt, even if the resulting frames are numerically identical.</param>
/// <param name="SourceStemId">The <c>StemAsset.Id</c> this analysis was computed from.</param>
/// <param name="SourceHash">The source <c>StemAsset.Sha256</c> at analysis time -- part of this analysis's own cache key.</param>
/// <param name="SourceSampleRate">The source stem's sample rate in Hz at analysis time.</param>
/// <param name="AnalyzerVersion">Identifies which pitch tracker (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="PitchTrackingSettings"/> this run used -- part of this analysis's own cache key.</param>
/// <param name="Frames">Raw per-hop pitch estimates, ordered by <see cref="PitchFrame.TimeTicks"/>, one per analyzed hop position -- never sparse (see <see cref="PitchFrame"/>'s own "preserve raw contour for fallback" remarks).</param>
public sealed record PitchTrackAnalysis(
    Guid VersionId,
    Guid SourceStemId,
    string SourceHash,
    int SourceSampleRate,
    string AnalyzerVersion,
    PitchTrackingSettings Settings,
    PitchFrame[] Frames);
