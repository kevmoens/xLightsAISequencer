using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 055's own persisted, versioned output: every segmented <see cref="PhraseCandidate"/> for one
/// song, tied back to the exact upstream <see cref="SectionLabelAnalysis"/>, <see cref="SongFingerprintSet"/>
/// and (optional) <c>MelodyTrack</c> revisions it was segmented from (Section 4: "every cache key includes
/// algorithm/model/settings versions," applied across the full provenance chain). This is the value an
/// <see cref="IPhraseSegmenter.SegmentAsync"/> caller actually receives -- the automatic segmentation
/// merged with any persisted <see cref="IPhraseBoundaryOverrideStore"/> entries for this song, which is why
/// <see cref="Phrases"/> can carry <see cref="PhraseCandidate.UserCorrected"/> entries even though this
/// record itself has no separate "raw" vs. "merged" distinction at the type level (Section 4: "Persist
/// detected structure and user-edited structure in separately versioned stores" -- the two stores are
/// separate; this is the one user-facing read model).
/// </summary>
/// <param name="VersionId">Identity of the underlying automatic segmentation run this merged result was built from -- changes only when the automatic recipe is rebuilt, not when a manual boundary correction is added or changed (the override store is versioned separately).</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> every upstream analysis was itself built from.</param>
/// <param name="SourceSectionLabelVersionId">The exact <see cref="SectionLabelAnalysis.VersionId"/> this analysis's phrases were segmented against -- a rebuilt (even numerically-identical) label analysis always invalidates this cache.</param>
/// <param name="SourceFingerprintSetVersionId">The exact <see cref="SongFingerprintSet.VersionId"/> this analysis's phrase signatures were computed from.</param>
/// <param name="SourceMelodyTrackId">The exact <c>MelodyTrack.Id</c> this analysis's cadence evidence was computed from, or <see langword="null"/> when no melody track was supplied for this run.</param>
/// <param name="AnalyzerVersion">Identifies which segmenter (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="PhraseSegmentationSettings"/> this run used.</param>
/// <param name="Phrases">Segmented phrases, ordered by <see cref="PhraseCandidate.StartBarIndex"/>. Always covers every upstream <see cref="SectionLabelAnalysis.Labels"/> entry's own bar range with one or more contiguous, non-overlapping phrases; empty, never an exception, when the source label analysis itself has no labels (silence).</param>
public sealed record PhraseSegmentationAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceSectionLabelVersionId,
    Guid SourceFingerprintSetVersionId,
    Guid? SourceMelodyTrackId,
    string AnalyzerVersion,
    PhraseSegmentationSettings Settings,
    IReadOnlyList<PhraseCandidate> Phrases);
