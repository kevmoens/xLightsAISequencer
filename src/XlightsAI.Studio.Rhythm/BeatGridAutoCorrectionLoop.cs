namespace XlightsAI.Studio.Rhythm;

/// <summary>PHASE-05 Step 037 — real implementation of <see cref="IBeatGridAutoCorrectionLoop"/>. See the interface's own remarks for the overall design.</summary>
public sealed class BeatGridAutoCorrectionLoop : IBeatGridAutoCorrectionLoop
{
    private readonly IBeatGridEditor _editor;
    private readonly IBeatGridQualityGate _qualityGate;

    public BeatGridAutoCorrectionLoop(IBeatGridEditor editor, IBeatGridQualityGate qualityGate)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _qualityGate = qualityGate ?? throw new ArgumentNullException(nameof(qualityGate));
    }

    public BeatGridCorrectionOutcome TryApply(
        BeatGrid grid,
        IReadOnlyList<BeatGridCorrectionProposal> proposals,
        BeatGridQualityGateSettings qualitySettings,
        int maxAttempts)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(qualitySettings);

        if (maxAttempts <= 0)
        {
            throw new BeatGridCorrectionInvalidException(grid.SongId, $"maxAttempts ({maxAttempts}) must be positive.");
        }

        var reportBefore = _qualityGate.Evaluate(grid, qualitySettings);

        if (proposals.Count == 0)
        {
            return new BeatGridCorrectionOutcome(grid, reportBefore, ReportAfter: null, AttemptsUsed: 0, Committed: false,
                RejectionReason: "No correction proposals were supplied.");
        }

        var budget = Math.Min(proposals.Count, maxAttempts);
        var attemptsUsed = 0;
        string rejectionReason = "No proposal was attempted.";

        for (var i = 0; i < budget; i++)
        {
            attemptsUsed++;
            var proposal = proposals[i];

            BeatGrid candidate;
            try
            {
                candidate = Apply(grid, proposal);
            }
            catch (BeatGridEditInvalidException ex)
            {
                // "Malformed proposal... must not auto-commit": an unknown beat id, a missing required
                // field or any other shape the real editor itself rejects is never silently applied —
                // skip it and keep the caller's remaining budget for the next candidate.
                rejectionReason = $"Proposal #{i} ({proposal.Kind} on beat '{proposal.BeatId}') was malformed and was not committed: {ex.Reason}";
                continue;
            }

            var candidateReport = _qualityGate.Evaluate(candidate, qualitySettings);
            if (candidateReport.OverallConfidence > reportBefore.OverallConfidence)
            {
                return new BeatGridCorrectionOutcome(candidate, reportBefore, candidateReport, attemptsUsed, Committed: true, RejectionReason: null);
            }

            // "...no-improvement... must not auto-commit": a structurally valid edit that does not
            // actually raise the grid's own measured confidence is discarded, not kept just because it
            // ran without throwing.
            rejectionReason = $"Proposal #{i} ({proposal.Kind} on beat '{proposal.BeatId}') did not improve overall confidence ({candidateReport.OverallConfidence:P0} <= {reportBefore.OverallConfidence:P0}) and was not committed.";
        }

        if (proposals.Count > maxAttempts)
        {
            // "...exhausted retry budget... must not auto-commit": more candidates existed than the
            // caller allowed trying — explicitly disclosed, not silently treated as "no candidates
            // existed."
            rejectionReason += " Retry budget exhausted before every supplied proposal could be tried.";
        }

        return new BeatGridCorrectionOutcome(grid, reportBefore, ReportAfter: null, attemptsUsed, Committed: false, rejectionReason);
    }

    private BeatGrid Apply(BeatGrid grid, BeatGridCorrectionProposal proposal) => proposal.Kind switch
    {
        BeatGridCorrectionProposalKind.MoveBeat => _editor.MoveBeat(
            grid,
            proposal.BeatId,
            proposal.NewTimeTicks ?? throw new BeatGridEditInvalidException(grid.SongId, "NewTimeTicks is required for a MoveBeat proposal."),
            grid.CorrectionVersion),
        BeatGridCorrectionProposalKind.SetDownbeat => _editor.SetDownbeat(grid, proposal.BeatId, grid.CorrectionVersion),
        _ => throw new BeatGridEditInvalidException(grid.SongId, $"Unknown proposal kind '{proposal.Kind}'."),
    };
}
