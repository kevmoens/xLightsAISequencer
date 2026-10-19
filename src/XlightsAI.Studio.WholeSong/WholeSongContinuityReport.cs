namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 093's own required acceptance, verbatim: "...passes continuity report." The full,
/// inspectable report one <see cref="ISongContinuityEvaluator.Evaluate"/> call produced, plus the single
/// boolean gate <see cref="WholeSongExportAuditor"/> uses to decide whether export may proceed.
/// </summary>
/// <param name="Findings">Every finding, in the order <see cref="ISongContinuityEvaluator.Evaluate"/> produced them -- never filtered or truncated here (Section 4: "no silent caps").</param>
public sealed record WholeSongContinuityReport(IReadOnlyList<ContinuityFinding> Findings)
{
    /// <summary>
    /// <see langword="true"/> only when no <see cref="Findings"/> entry carries
    /// <see cref="ContinuitySeverity.Error"/>. <see cref="ContinuitySeverity.Warning"/> findings are
    /// surfaced for human review but never block export on their own.
    /// </summary>
    public bool Passed => !Findings.Any(f => f.Severity == ContinuitySeverity.Error);
}
