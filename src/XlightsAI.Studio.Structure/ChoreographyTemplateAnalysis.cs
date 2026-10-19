using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 057's own persisted, versioned output: every <see cref="ChoreographyTemplate"/> (and its own
/// <see cref="ChoreographyInstance"/> overrides) defined for one song, tied back to the exact upstream
/// <see cref="MotifClusteringAnalysis"/> revision and source <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/>
/// revision every template's own captured effects were built from (Section 4: "every cache key includes
/// algorithm/model/settings versions," applied across the full provenance chain). <see cref="VersionId"/> is
/// also this record's own optimistic-concurrency token: <see cref="IChoreographyTemplatePlanner"/> rejects
/// any write whose caller-supplied "current" analysis does not match what is actually persisted (Step 057's
/// own "stale persisted state must not corrupt the project" failure/edge case, verbatim).
/// </summary>
/// <param name="VersionId">Identity of this particular persisted revision -- changes every time any template or instance is defined/refreshed/overridden.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis belongs to.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> every upstream analysis was itself built from.</param>
/// <param name="SourceMotifClusteringVersionId">The exact <see cref="MotifClusteringAnalysis.VersionId"/> every template's own <see cref="ChoreographyTemplate.MotifGroupId"/>/<see cref="ChoreographyTemplate.SourcePhraseId"/> was validated against at the time it was (re)defined.</param>
/// <param name="SourceDocumentRevision">The exact <see cref="XlightsAI.Studio.Xlights.SequenceDocument.Revision"/> every template's own <see cref="ChoreographyTemplate.Effects"/> were captured from.</param>
/// <param name="AnalyzerVersion">Identifies which planner (and its version) produced this analysis.</param>
/// <param name="Templates">Every defined template for this song, one per distinct <c>(MotifGroupId, SourcePhraseId)</c> pair.</param>
public sealed record ChoreographyTemplateAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceMotifClusteringVersionId,
    long SourceDocumentRevision,
    string AnalyzerVersion,
    IReadOnlyList<ChoreographyTemplate> Templates);
