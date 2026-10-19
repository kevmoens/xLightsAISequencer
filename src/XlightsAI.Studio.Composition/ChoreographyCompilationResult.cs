using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 071's own full compilation outcome: the resulting <see cref="SequenceDocument"/> (the
/// caller's own base document plus every admitted candidate's own newly compiled effect, with every
/// pre-existing effect carried forward byte-for-byte unchanged) plus every input candidate's own
/// resolved outcome, plus non-fatal advisory notes.
/// </summary>
/// <param name="Document">The caller's own <see cref="ChoreographyCompiler.Compile"/>'s base document, unchanged if nothing was admitted, or a new revision carrying every admitted candidate's own compiled effect if at least one was.</param>
/// <param name="Results">Every input candidate's own resolved outcome, in the same relative order <see cref="IChoreographyCompiler.Compile"/> received them.</param>
/// <param name="Warnings">Non-fatal observations -- one entry per <see cref="ChoreographyResolutionDecision.Reassigned"/>/<see cref="ChoreographyResolutionDecision.Dropped"/> candidate, disclosing exactly why ("purposeful coordination", this step's own implementation guidance, verbatim, made inspectable rather than silent).</param>
public sealed record ChoreographyCompilationResult(
    SequenceDocument Document,
    IReadOnlyList<ChoreographyCandidateResult> Results,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Every result whose <see cref="ChoreographyCandidateResult.Decision"/> is not <see cref="ChoreographyResolutionDecision.Dropped"/>.</summary>
    public IEnumerable<ChoreographyCandidateResult> Admitted => Results.Where(r => r.Decision != ChoreographyResolutionDecision.Dropped);

    /// <summary>Every result whose <see cref="ChoreographyCandidateResult.Decision"/> is <see cref="ChoreographyResolutionDecision.Dropped"/>.</summary>
    public IEnumerable<ChoreographyCandidateResult> Dropped => Results.Where(r => r.Decision == ChoreographyResolutionDecision.Dropped);
}
