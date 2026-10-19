using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 033's own persisted, versioned output: a ranked set of tempo candidates derived from an already
/// built <see cref="OnsetAnalysis"/> (Section 2: "Separate raw audio features from human-approved beat
/// grid" — this is still a raw analysis artifact, not a user-approved <c>BeatGrid</c>). Mirrors
/// <see cref="OnsetAnalysis"/>'s own provenance shape, with <see cref="SourceOnsetAnalysisVersionId"/>
/// added as this step's own cache-key link back to the specific onset run it was built from (Steps
/// 034+ will add a <c>BeatGrid</c> that in turn references this analysis).
/// </summary>
/// <param name="VersionId">Identity of this particular computed run — changes every time the analysis is rebuilt, even if the resulting candidates are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis was ultimately computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> the consumed <see cref="OnsetAnalysis"/> was itself built from.</param>
/// <param name="SourceOnsetAnalysisVersionId">The exact <see cref="OnsetAnalysis.VersionId"/> this tempo analysis was derived from — a rebuilt onset analysis (even with the same <see cref="SourceHash"/>, e.g. a changed <see cref="OnsetDetectionSettings"/>) always invalidates this cache.</param>
/// <param name="AnalyzerVersion">Identifies which detector (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="TempoDetectionSettings"/> scan this run used.</param>
/// <param name="Candidates">Ranked tempo candidates, ordered by <see cref="TempoHypothesis.Confidence"/> descending. Empty when the source onset analysis carried no onsets (e.g. silence) — a disclosed, explicit result, never an exception, for that case.</param>
public sealed record TempoAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceOnsetAnalysisVersionId,
    string AnalyzerVersion,
    TempoDetectionSettings Settings,
    TempoHypothesis[] Candidates);
