using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14.md Section 3's own proposed contract, kept exactly as-is (no deviation): one concrete,
/// time/prop-located continuity problem <see cref="ISongContinuityEvaluator.Evaluate"/> found -- "Expose a
/// song-level overview that links anomalies directly to timeline locations" (Section 3's own architectural
/// design note, verbatim).
/// </summary>
/// <param name="Code">One of <see cref="ContinuityFindingCode"/>'s named values.</param>
/// <param name="Severity">One of <see cref="ContinuitySeverity"/>'s named values.</param>
/// <param name="Window">The exact half-open tick range this finding concerns -- the "specific time... links" PHASE-14.md Section 3 requires.</param>
/// <param name="Models">Every model/group this finding concerns -- the "...prop links" PHASE-14.md Section 3 requires. May be empty when a finding is not resolvable to a specific prop from the inputs available (disclosed per-finding, never silently guessed).</param>
/// <param name="Explanation">A human-readable explanation, safe to surface directly on SCREEN 03/12/13.</param>
public sealed record ContinuityFinding(
    string Code,
    string Severity,
    TimeWindow Window,
    IReadOnlyList<ModelId> Models,
    string Explanation);
