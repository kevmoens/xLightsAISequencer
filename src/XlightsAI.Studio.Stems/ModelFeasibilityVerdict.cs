namespace XlightsAI.Studio.Stems;

/// <summary>
/// Per-candidate feasibility verdict for Step 038's own required acceptance ("at least two feasible
/// alternatives or explicit blocker"). <see cref="Feasible"/> is a narrow, load-bearing claim: both
/// <see cref="ModelFeasibilityCandidate.VerifiedExecutableUnderDotNet"/> and
/// <see cref="ModelFeasibilityCandidate.LicenseRedistributable"/> must independently be true before a
/// candidate may carry this verdict (enforced by <see cref="ModelFeasibilityEvaluator"/>, not merely
/// asserted by the caller) — Section 2's own "Must prove, don't infer" rule applied to this step.
/// </summary>
public enum ModelFeasibilityVerdict
{
    /// <summary>Proven executable under .NET with a redistributable license; counts toward the
    /// required "two feasible alternatives" acceptance threshold.</summary>
    Feasible,

    /// <summary>A known, disclosed blocker (license, runtime, or quality) rules this candidate out.</summary>
    NotFeasible,

    /// <summary>Inspected but not yet proven either way (e.g. awaiting a benchmark run); never counts
    /// toward the feasible threshold.</summary>
    Unproven,

    /// <summary>
    /// New, PHASE-06A Step 044F. Proven <see cref="ModelFeasibilityCandidate.VerifiedExecutableUnderDotNet"/>
    /// == true, but <see cref="ModelFeasibilityCandidate.LicenseRedistributable"/> is genuinely contested
    /// rather than cleanly true or false (the real, disclosed state of the sherpa-onnx Spleeter pretrained
    /// weights — see PHASE-06A.md §3/§7/§8 and <c>implementation-evidence/PHASE-06A/STEP-044F.md</c>). Never
    /// counts toward the "two feasible alternatives" acceptance threshold <see cref="ModelFeasibilityEvaluator"/>
    /// enforces — deliberately a lesser, more honest claim than <see cref="Feasible"/>, not a quiet weakening
    /// of it.
    ///
    /// <b>Appended last, not inserted mid-sequence, on purpose.</b> <see cref="JsonModelFeasibilityReportStore"/>
    /// serializes this enum via plain <c>JsonSerializer</c> with no <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>
    /// — i.e. as a raw integer (<see cref="Feasible"/>=0, <see cref="NotFeasible"/>=1, <see cref="Unproven"/>=2).
    /// Inserting a new member before <see cref="Unproven"/> would silently reinterpret any already-persisted
    /// ordinal <c>2</c> as whatever member now occupies that ordinal. Appending strictly after
    /// <see cref="Unproven"/> is the only safe insertion point for this ordinal-serialized enum.
    /// </summary>
    FeasibleWithDisclosedLicenseRisk,
}
