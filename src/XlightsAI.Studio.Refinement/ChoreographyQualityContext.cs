using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// Optional, already-accepted analysis inputs an <see cref="IChoreographyQualityEvaluator"/> may use to
/// score the soft "timing," "melodic alignment," and "motif reuse" dimensions (this step's own heading)
/// against real music analysis rather than the candidate's own gestures alone. Every field is nullable and
/// independently optional -- this step's own required failure/edge case, verbatim: "Silence, invalid timing
/// boundaries and missing media must be handled explicitly." A caller who has not yet run beat-grid/melody
/// analysis for a song (or whose song is silent) passes <see cref="Empty"/> (or leaves any one field
/// unset); the evaluator reports that dimension's own <see cref="MetricValue"/> with <c>Weight = 0</c> and
/// an explicit explanation, rather than fabricating a confident-looking score from absent data.
/// </summary>
/// <param name="BeatGrid">PHASE-05's already-accepted analyzed beat grid for the song this run's document is set to, or <see langword="null"/> when no beat analysis has been run (or the song is silent -- <see cref="BeatGrid.Beats"/> may itself be empty even when this field is set).</param>
/// <param name="Melody">PHASE-07's already-accepted melody track for the song this run's document is set to, or <see langword="null"/> when no melody analysis has been run (or the song is silent -- <see cref="MelodyTrack.Notes"/> may itself be empty even when this field is set).</param>
/// <param name="LockedEffects">Every already-committed, locked <see cref="EffectInstance"/> in the document this run refines -- PHASE-03's own effect-lock concern, reused here so the "legal effects" hard gate can detect a generated gesture that would silently overwrite a human-approved, locked effect.</param>
/// <param name="PriorAttempts">Every previously recorded, non-violating <see cref="CandidateAttempt"/> for this same run -- used by the "motif reuse" metric to score this candidate's own gesture shape against what the run has already produced.</param>
public sealed record ChoreographyQualityContext(
    BeatGrid? BeatGrid = null,
    MelodyTrack? Melody = null,
    IReadOnlyList<EffectInstance>? LockedEffects = null,
    IReadOnlyList<CandidateAttempt>? PriorAttempts = null)
{
    /// <summary>No analysis inputs supplied at all -- every soft metric that depends on one reports <c>Weight = 0</c> with an explicit "no data" explanation.</summary>
    public static readonly ChoreographyQualityContext Empty = new();
}
