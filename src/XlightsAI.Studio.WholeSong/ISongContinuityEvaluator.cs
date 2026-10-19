using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14.md Section 3's own proposed contract, narrowed to this repository's own already-accepted
/// Step 090/091/092 output shapes rather than the spec's single combined
/// <c>Evaluate(SequenceDocument candidate, WholeSongPlan plan, SongStructure structure)</c> (disclosed
/// deviation, the same "narrower, explicitly-scoped realization" convention
/// <c>SongDirectionPlan</c>/<c>PhraseAssemblyResult</c>/<c>MotifVariationPlan</c> already established for
/// their own proposed Section-3 counterparts): <paramref name="seams"/> is Step 091's own real
/// <see cref="PhraseSeamPlan"/> output, and <paramref name="motifPlan"/> is Step 092's own real
/// <see cref="MotifVariationPlan"/> output (nullable -- a song with no motif reuse yet has none).
/// "Run continuity evaluation across all phrase boundaries and sections" (this step's own implementation
/// guidance, verbatim).
/// </summary>
public interface ISongContinuityEvaluator
{
    /// <param name="candidate">The whole-song candidate document to scan for genuine, unresolved cross-effect overlaps.</param>
    /// <param name="seams">Every <see cref="PhraseSeamPlan"/> from the assembly that produced <paramref name="candidate"/> (Step 091's own output) -- re-surfaced here at the whole-song review stage.</param>
    /// <param name="motifPlan">Step 092's own scheduled motif-variation plan, or <see langword="null"/> when this song does not yet reuse any motif.</param>
    /// <param name="currentTemplateAnalysisVersionId">The exact, currently-known <c>ChoreographyTemplateAnalysis.VersionId</c> to compare <paramref name="motifPlan"/>'s own <c>SourceTemplateAnalysisVersionId</c> against, or <see langword="null"/> when no current version is known (staleness is then never checked).</param>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> or <paramref name="seams"/> is <see langword="null"/>.</exception>
    IReadOnlyList<ContinuityFinding> Evaluate(
        SequenceDocument candidate,
        IReadOnlyList<PhraseSeamPlan> seams,
        MotifVariationPlan? motifPlan,
        Guid? currentTemplateAnalysisVersionId,
        CancellationToken ct);
}
