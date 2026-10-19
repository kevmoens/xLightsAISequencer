using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 093 ("Run whole-song rhythm/contrast/complexity checks; export and audition"): the whole-
/// song user gate this step's own implementation guidance requires, verbatim: "...provide specific time/
/// prop links and a whole-song user gate." Glues the real, already-accepted <see cref="ISongContinuityEvaluator"/>
/// (rhythm/contrast/complexity checks, expressed as the real seam/motif consistency findings this phase's
/// own Steps 091/092 already produce) to the real, already-accepted PHASE-02 <see cref="IXsqExporter"/>
/// (export) and <see cref="IXlightsLauncher"/> (audition) -- neither adapter is re-implemented here.
/// </summary>
public interface IWholeSongExportAuditor
{
    /// <param name="layout">The show layout the exported <c>.xsq</c> is written against.</param>
    /// <param name="candidate">The whole-song candidate document to evaluate and export.</param>
    /// <param name="seams">Every seam from the assembly that produced <paramref name="candidate"/> (Step 091's own output).</param>
    /// <param name="motifPlan">Step 092's own scheduled motif-variation plan, or <see langword="null"/> when this song does not yet reuse any motif.</param>
    /// <param name="currentTemplateAnalysisVersionId">See <see cref="ISongContinuityEvaluator.Evaluate"/>.</param>
    /// <param name="outputPath">The <c>.xsq</c> output path -- never an existing source show file (Section 2: "Never overwrite source show files silently").</param>
    /// <param name="targetXlightsVersion">The target xLights version recorded in the exported file's own <c>head</c>.</param>
    /// <param name="policy">How to resolve an already-existing <paramref name="outputPath"/> -- see <see cref="XsqExportPolicy"/>.</param>
    /// <param name="xlightsExecutablePathForAudition">An installed xLights executable path to open the export in for a human audition, or <see langword="null"/>/blank to skip audition entirely (disclosed, never faked, when no installed xLights is available).</param>
    /// <exception cref="SongContinuityBlockedException">The continuity report did not pass (at least one <see cref="ContinuitySeverity.Error"/> finding). No file is written.</exception>
    /// <exception cref="XlightsAI.Studio.Xlights.XsqExportInvalidException">The export request itself is invalid -- see <see cref="IXsqExporter"/>. No file is written.</exception>
    /// <exception cref="XlightsLaunchInvalidException"><paramref name="xlightsExecutablePathForAudition"/> was supplied but does not exist, or the configured executable could not be started. The export itself has already succeeded and is never rolled back by an audition failure.</exception>
    Task<WholeSongExportAuditResult> ExportAndAuditAsync(
        DisplayGraph layout,
        SequenceDocument candidate,
        IReadOnlyList<PhraseSeamPlan> seams,
        MotifVariationPlan? motifPlan,
        Guid? currentTemplateAnalysisVersionId,
        string outputPath,
        string targetXlightsVersion,
        XsqExportPolicy policy,
        string? xlightsExecutablePathForAudition,
        CancellationToken ct);
}
