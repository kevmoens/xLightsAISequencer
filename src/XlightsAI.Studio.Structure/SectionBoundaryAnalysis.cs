using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Step 053's own persisted, versioned output: every detected <see cref="SectionBoundaryCandidate"/>
/// for one song plus the honest <see cref="ErrorReport"/> of how the detection run actually behaved,
/// tied back to the exact <see cref="SongFingerprintSet"/> revision it was computed from (Section 4:
/// "every cache key includes algorithm/model/settings versions," applied across the full provenance
/// chain).
/// </summary>
/// <param name="VersionId">Identity of this particular computed run -- changes every time the analysis is rebuilt, even if the resulting boundaries are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this analysis was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> the consumed <see cref="SongFingerprintSet"/> was itself built from.</param>
/// <param name="SourceFingerprintSetVersionId">The exact <see cref="SongFingerprintSet.VersionId"/> this analysis's candidates were detected against -- a rebuilt (even numerically-identical) fingerprint set always invalidates this cache.</param>
/// <param name="AnalyzerVersion">Identifies which detector (and its version) produced this analysis.</param>
/// <param name="Settings">The exact <see cref="SectionBoundarySettings"/> this run used.</param>
/// <param name="Boundaries">Detected candidates, ordered by <see cref="SectionBoundaryCandidate.BarIndex"/>. Always starts with the trivial bar-<c>0</c> entry when the source fingerprint set has at least one bar; empty, never an exception, when the source set itself has no bars (silence).</param>
/// <param name="ErrorReport">Step 053's own required acceptance, verbatim: "...and error report." Never omitted, never fabricated as a clean pass when the search did not actually converge.</param>
public sealed record SectionBoundaryAnalysis(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    Guid SourceFingerprintSetVersionId,
    string AnalyzerVersion,
    SectionBoundarySettings Settings,
    IReadOnlyList<SectionBoundaryCandidate> Boundaries,
    SectionBoundaryErrorReport ErrorReport);
