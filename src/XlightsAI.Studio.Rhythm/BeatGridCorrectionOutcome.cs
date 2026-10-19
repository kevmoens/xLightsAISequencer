namespace XlightsAI.Studio.Rhythm;

/// <param name="Grid">The grid actually returned — the original, completely unmodified grid supplied to <see cref="IBeatGridAutoCorrectionLoop.TryApply"/> when <see cref="Committed"/> is <see langword="false"/> (this step's own "must not auto-commit" rule), or the edited grid when <see cref="Committed"/> is <see langword="true"/>.</param>
/// <param name="ReportBefore">The quality report computed from the grid supplied to <see cref="IBeatGridAutoCorrectionLoop.TryApply"/>, before any proposal was attempted.</param>
/// <param name="ReportAfter">The quality report computed from <see cref="Grid"/> after a committed correction, or <see langword="null"/> when nothing was committed.</param>
/// <param name="AttemptsUsed">How many proposals were actually tried (a malformed proposal still counts as a spent attempt) before stopping — bounded by the caller's own retry budget.</param>
/// <param name="Committed">Whether a proposal was actually applied.</param>
/// <param name="RejectionReason">Disclosed, human-readable reason nothing was committed — <see langword="null"/> only when <see cref="Committed"/> is <see langword="true"/>.</param>
public sealed record BeatGridCorrectionOutcome(
    BeatGrid Grid,
    BeatGridQualityReport ReportBefore,
    BeatGridQualityReport? ReportAfter,
    int AttemptsUsed,
    bool Committed,
    string? RejectionReason);
