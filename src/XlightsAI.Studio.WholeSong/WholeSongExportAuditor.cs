using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="IWholeSongExportAuditor"/>. See the interface for the full contract.</summary>
public sealed class WholeSongExportAuditor : IWholeSongExportAuditor
{
    private readonly ISongContinuityEvaluator _evaluator;
    private readonly IXsqExporter _exporter;
    private readonly IXlightsLauncher _launcher;

    public WholeSongExportAuditor(ISongContinuityEvaluator evaluator, IXsqExporter exporter, IXlightsLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(launcher);
        _evaluator = evaluator;
        _exporter = exporter;
        _launcher = launcher;
    }

    public async Task<WholeSongExportAuditResult> ExportAndAuditAsync(
        DisplayGraph layout,
        SequenceDocument candidate,
        IReadOnlyList<PhraseSeamPlan> seams,
        MotifVariationPlan? motifPlan,
        Guid? currentTemplateAnalysisVersionId,
        string outputPath,
        string targetXlightsVersion,
        XsqExportPolicy policy,
        string? xlightsExecutablePathForAudition,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(seams);

        // Checked first, before the (pure, in-memory) continuity evaluation and well before any file I/O --
        // this step's own required failure/edge case, verbatim: "...cancellation... must not corrupt the
        // project."
        ct.ThrowIfCancellationRequested();

        var findings = _evaluator.Evaluate(candidate, seams, motifPlan, currentTemplateAnalysisVersionId, ct);
        var report = new WholeSongContinuityReport(findings);

        if (!report.Passed)
        {
            // "Export opens in xLights and passes continuity report" (required acceptance, verbatim): a
            // failed report never reaches IXsqExporter -- no file is ever written for this attempt.
            throw new SongContinuityBlockedException(findings.Where(f => f.Severity == ContinuitySeverity.Error).ToList());
        }

        var export = await _exporter.ExportAsync(layout, candidate, outputPath, targetXlightsVersion, policy, ct)
            .ConfigureAwait(false);

        XlightsLaunchResult? audition = null;
        if (!string.IsNullOrWhiteSpace(xlightsExecutablePathForAudition))
        {
            // "...export and audition" (this step's own title, verbatim). The export above has already
            // succeeded and is never rolled back if this launch itself fails (PHASE-02 Step 014's own
            // established "launch never substitutes for, nor blocks, a real export" convention).
            audition = await _launcher.LaunchAsync(xlightsExecutablePathForAudition, export.Path, ct)
                .ConfigureAwait(false);
        }

        return new WholeSongExportAuditResult(report, export, audition);
    }
}
