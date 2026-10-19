namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 093: every <see cref="ContinuityFinding.Code"/> value <see cref="SongContinuityEvaluator"/>
/// currently emits -- named constants rather than free-form strings scattered through the evaluator, the
/// same "never an invented free-form value when a closed set exists" convention
/// <c>XlightsAI.Studio.Display.RouteIssueKind</c> already established.
/// </summary>
public static class ContinuityFindingCode
{
    /// <summary>
    /// <see cref="ContinuitySeverity.Error"/>. Two effects on the same target+layer genuinely overlap in
    /// the final candidate document -- this step's own defensive, direct realization of the phase test
    /// matrix's own row 4, "Cross-phrase overlapping effect is flagged for review", applied to whatever
    /// document is actually handed to <see cref="ISongContinuityEvaluator.Evaluate"/> (not only one that
    /// already passed through <see cref="PhraseSeamAssembler"/>, which never emits this case itself).
    /// </summary>
    public const string UnresolvedOverlap = "UNRESOLVED_OVERLAP";

    /// <summary>
    /// <see cref="ContinuitySeverity.Warning"/>. A <see cref="PhraseSeamPlan.Warnings"/> entry disclosed
    /// that a cross-phrase overlap was clipped or dropped to resolve a seam -- phase test matrix row 4,
    /// surfaced at the whole-song review stage rather than only in Step 091's own per-assembly warnings.
    /// </summary>
    public const string SeamOverlapResolved = "SEAM_OVERLAP_RESOLVED";

    /// <summary>
    /// <see cref="ContinuitySeverity.Warning"/>. A <see cref="PhraseSeamPlan.Warnings"/> entry disclosed a
    /// remaining unbridged gap -- phase test matrix row 3, "A silence bridge is not filled indiscriminately":
    /// the gap is surfaced for review, never silently filled.
    /// </summary>
    public const string UnresolvedGap = "UNRESOLVED_GAP";

    /// <summary>
    /// <see cref="ContinuitySeverity.Error"/>. The supplied <c>MotifVariationPlan.SourceTemplateAnalysisVersionId</c>
    /// no longer matches the caller-supplied current template-analysis version -- phase test matrix row 5,
    /// "Motif source changes generate stale-instance warning". Blocks export: a stale motif plan must be
    /// re-scheduled against the current template analysis before the whole song is exported.
    /// </summary>
    public const string MotifSourceStale = "MOTIF_SOURCE_STALE";

    /// <summary>
    /// <see cref="ContinuitySeverity.Warning"/>. One <c>MotifOccurrenceVariation.ExcludedReasons</c> entry
    /// is non-empty -- a template-effect slot was excluded from this occurrence (a locked destination
    /// conflict, or a non-fitting timing window), surfaced here again at the whole-song review stage.
    /// </summary>
    public const string MotifInstanceExcluded = "MOTIF_INSTANCE_EXCLUDED";

    /// <summary>
    /// <see cref="ContinuitySeverity.Error"/>. A motif group's own supplied occurrences do not satisfy
    /// <c>MotifVariationScheduler</c>'s own "climax strictly exceeds every other same-template occurrence"
    /// guarantee -- phase test matrix row 6, "Section-to-section brightness arc is computed consistently".
    /// A hand-assembled or corrupted <c>MotifVariationPlan</c> can violate this even though the real
    /// scheduler itself never produces it; this is a final, defensive consistency check at export time.
    /// </summary>
    public const string ClimaxLiftInconsistent = "CLIMAX_LIFT_INCONSISTENT";
}
