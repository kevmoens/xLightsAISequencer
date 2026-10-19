using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 056's own persisted, versioned output: every <see cref="MotifGroupCandidate"/> and
/// <see cref="MotifLinkCandidate"/> computed for one song, tied back to the exact upstream
/// <see cref="PhraseSegmentationAnalysis"/> and <see cref="SongFingerprintSet"/> revisions it was clustered
/// from (Section 4: "every cache key includes algorithm/model/settings versions," applied across the full
/// provenance chain).
/// </summary>
/// <param name="VersionId">Identity of this particular computed run -- changes every time the set is rebuilt, even if the resulting groups/links are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> every upstream analysis was itself built from.</param>
/// <param name="SourcePhraseSegmentationVersionId">The exact <see cref="PhraseSegmentationAnalysis.VersionId"/> this analysis's phrases were clustered from -- a rebuilt (even numerically-identical) phrase segmentation always invalidates this cache.</param>
/// <param name="SourceFingerprintSetVersionId">The exact <see cref="SongFingerprintSet.VersionId"/> this analysis's phrase embeddings were resampled from.</param>
/// <param name="AnalyzerVersion">Identifies which clusterer (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="MotifClusteringSettings"/> this run used.</param>
/// <param name="Groups">Computed motif groups, ordered by each group's own <see cref="MotifGroupCandidate.RepresentativePhraseId"/>'s <see cref="PhraseCandidate.StartBarIndex"/>. Empty, never an exception, when no two phrases were judged similar enough to share a motif.</param>
/// <param name="Links">Every pairwise proposal at or above <see cref="MotifClusteringSettings.SimilarityThreshold"/> that produced <see cref="Groups"/>, ordered by the source phrase's own <see cref="PhraseCandidate.StartBarIndex"/> then the target's -- kept for inspection even though only connected-component membership (not the individual link list) is what <see cref="Groups"/> itself exposes.</param>
public sealed record MotifClusteringAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourcePhraseSegmentationVersionId,
    Guid SourceFingerprintSetVersionId,
    string AnalyzerVersion,
    MotifClusteringSettings Settings,
    IReadOnlyList<MotifGroupCandidate> Groups,
    IReadOnlyList<MotifLinkCandidate> Links);
