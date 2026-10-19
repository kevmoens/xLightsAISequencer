namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// PHASE-14 Step 092 ("Apply motif variations by occurrence and climax scheduling") own input shape: one
/// occurrence of an already-defined PHASE-08 Step 057 <c>ChoreographyTemplate</c> that should be previewed,
/// intensity-varied and (possibly) scheduled as the motif's own climax occurrence by
/// <see cref="IMotifVariationScheduler.Schedule"/>. <see cref="SectionEnergy"/> is the caller-supplied
/// section-level energy this occurrence sits in (e.g. a <c>SongSectionDirection.TargetEnergy</c> from Step
/// 090's own <c>SongDirectionPlan</c>) -- the scheduler itself never derives it, consistent with this
/// repository's own "AI/system proposes, user approves" convention: the caller is the one authority on which
/// section a phrase belongs to and how intense that section's own creative direction is.
/// </summary>
/// <param name="TemplateId">The owning <c>ChoreographyTemplate.Id</c> this occurrence adapts.</param>
/// <param name="TargetPhraseId">Which phrase in the template's own motif group this occurrence targets -- same meaning as <c>ChoreographyInstance.TargetPhraseId</c>.</param>
/// <param name="SectionEnergy">This occurrence's own section-level target energy in <c>[0,1]</c> -- the raw input <see cref="IMotifVariationScheduler.Schedule"/> both intensity-scales from and uses to decide, per <see cref="TemplateId"/>, which single occurrence becomes the climax ("climax scheduling," this step's own title, verbatim).</param>
public sealed record MotifOccurrenceRequest(
    Guid TemplateId,
    Guid TargetPhraseId,
    double SectionEnergy)
{
    /// <summary>
    /// Structural self-validation with no external dependency -- mirrors <c>PhraseAssemblyCandidate.Validate()</c>'s
    /// own convention. Cross-reference validation against a specific template/motif group is
    /// <see cref="IMotifVariationScheduler.Schedule"/>'s own separate responsibility.
    /// </summary>
    /// <exception cref="MotifVariationInvalidException"><see cref="TemplateId"/> or <see cref="TargetPhraseId"/> is empty, or <see cref="SectionEnergy"/> is not a finite number in <c>[0,1]</c>.</exception>
    public void Validate()
    {
        if (TemplateId == Guid.Empty)
        {
            throw new MotifVariationInvalidException("A motif occurrence request's TemplateId must not be empty.");
        }

        if (TargetPhraseId == Guid.Empty)
        {
            throw new MotifVariationInvalidException("A motif occurrence request's TargetPhraseId must not be empty.");
        }

        if (!double.IsFinite(SectionEnergy) || SectionEnergy is < 0.0 or > 1.0)
        {
            throw new MotifVariationInvalidException(
                $"Occurrence (template '{TemplateId}', phrase '{TargetPhraseId}') SectionEnergy must be a finite " +
                $"number in [0,1] (was {SectionEnergy}).");
        }
    }
}
