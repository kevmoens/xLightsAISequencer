using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 054's own persisted, versioned output: every classified <see cref="SectionLabelCandidate"/> for
/// one song, tied back to the exact upstream <see cref="SectionBoundaryAnalysis"/> and
/// <see cref="SongFingerprintSet"/> revisions it was classified from (Section 4: "every cache key
/// includes algorithm/model/settings versions," applied across the full provenance chain). This is the
/// value an <see cref="ISectionLabelClassifier.ClassifyAsync"/> caller actually receives -- the automatic
/// classification merged with any persisted <see cref="ISectionLabelOverrideStore"/> entries for this
/// song, which is why <see cref="Labels"/> can carry <see cref="SectionLabelCandidate.UserCorrected"/>
/// entries even though this record itself has no separate "raw" vs. "merged" distinction at the type
/// level (Section 4: "Persist detected structure and user-edited structure in separately versioned
/// stores" -- the two stores are separate; this is the one user-facing read model).
/// </summary>
/// <param name="VersionId">Identity of the underlying automatic classification run this merged result was built from -- changes only when the automatic recipe is rebuilt, not when a user override is added or changed (the override store is versioned separately).</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> both upstream analyses were themselves built from.</param>
/// <param name="SourceSectionBoundaryVersionId">The exact <see cref="SectionBoundaryAnalysis.VersionId"/> this analysis's segments were classified against -- a rebuilt (even numerically-identical) boundary analysis always invalidates this cache.</param>
/// <param name="SourceFingerprintSetVersionId">The exact <see cref="SongFingerprintSet.VersionId"/> this analysis's evidence was computed from.</param>
/// <param name="AnalyzerVersion">Identifies which classifier (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="SectionLabelSettings"/> this run used.</param>
/// <param name="Labels">Classified segments, ordered by <see cref="SectionLabelCandidate.StartBarIndex"/>. Always one entry per upstream <see cref="SectionBoundaryAnalysis.Boundaries"/> entry; empty, never an exception, when the source boundary analysis itself has no boundaries (silence).</param>
public sealed record SectionLabelAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceSectionBoundaryVersionId,
    Guid SourceFingerprintSetVersionId,
    string AnalyzerVersion,
    SectionLabelSettings Settings,
    IReadOnlyList<SectionLabelCandidate> Labels);
