using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 092's own versioned, inspectable output: every scheduled <see cref="MotifOccurrenceVariation"/>
/// produced by one <see cref="IMotifVariationScheduler.Schedule"/> call, tied back to the exact upstream
/// PHASE-08 Step 057 <c>ChoreographyTemplateAnalysis</c> and <c>PhraseSegmentationAnalysis</c> revisions it
/// was computed from (Section 4: "every cache key includes algorithm/model/settings versions," applied
/// across the full provenance chain) -- the same convention <c>SongDirectionPlan</c>/<c>ChoreographyTemplateAnalysis</c>
/// already establish.
/// </summary>
/// <param name="Id">A fresh identity minted every time a plan is (re)scheduled -- never reused across two independently scheduled plans, even over identical inputs.</param>
/// <param name="SongId">The <c>SongAsset.Id</c>/<see cref="SongId"/> this plan was created for.</param>
/// <param name="SourceTemplateAnalysisVersionId">The exact <c>ChoreographyTemplateAnalysis.VersionId</c> every occurrence's own template/instance data was read from.</param>
/// <param name="SourcePhraseSegmentationVersionId">The exact <c>PhraseSegmentationAnalysis.VersionId</c> every occurrence's own target-phrase timing was read from.</param>
/// <param name="CreatedAtUtc">The exact timestamp this plan was created.</param>
/// <param name="Occurrences">Every occurrence's own scheduled result, in the same order <see cref="MotifOccurrenceRequest"/> entries were supplied in.</param>
public sealed record MotifVariationPlan(
    Guid Id,
    SongId SongId,
    Guid SourceTemplateAnalysisVersionId,
    Guid SourcePhraseSegmentationVersionId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<MotifOccurrenceVariation> Occurrences)
{
    /// <summary>
    /// Structural self-validation with no external dependency -- mirrors <c>SongDirectionPlan.Validate()</c>'s
    /// own convention.
    /// </summary>
    /// <exception cref="MotifVariationInvalidException"><see cref="Occurrences"/> is empty, contains a duplicate <c>(TemplateId, TargetPhraseId)</c> pair, or any entry's own <see cref="MotifOccurrenceVariation.IntensityMultiplier"/> falls outside <see cref="MotifVariationScheduler.MinMultiplier"/>..<see cref="MotifVariationScheduler.MaxMultiplier"/>.</exception>
    public void Validate()
    {
        if (Occurrences.Count == 0)
        {
            throw new MotifVariationInvalidException($"MotifVariationPlan '{Id}' must contain at least one occurrence.");
        }

        var seen = new HashSet<(Guid TemplateId, Guid TargetPhraseId)>();
        foreach (var occurrence in Occurrences)
        {
            if (!seen.Add((occurrence.TemplateId, occurrence.TargetPhraseId)))
            {
                throw new MotifVariationInvalidException(
                    $"Duplicate occurrence (template '{occurrence.TemplateId}', phrase '{occurrence.TargetPhraseId}') " +
                    $"in MotifVariationPlan '{Id}'.");
            }

            if (!double.IsFinite(occurrence.IntensityMultiplier)
                || occurrence.IntensityMultiplier < MotifVariationScheduler.MinMultiplier
                || occurrence.IntensityMultiplier > MotifVariationScheduler.MaxMultiplier)
            {
                throw new MotifVariationInvalidException(
                    $"Occurrence (template '{occurrence.TemplateId}', phrase '{occurrence.TargetPhraseId}') " +
                    $"IntensityMultiplier ({occurrence.IntensityMultiplier}) must lie within " +
                    $"[{MotifVariationScheduler.MinMultiplier},{MotifVariationScheduler.MaxMultiplier}].");
            }
        }
    }
}
