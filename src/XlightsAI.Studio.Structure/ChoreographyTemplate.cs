namespace XlightsAI.Studio.Structure;

/// <summary>
/// The "parent" half of Step 057's own title, verbatim: "parent/instance pattern inheritance with
/// adjustable mask and transform." One approved motif occurrence's own captured effect structure, the
/// inheritance mask every other occurrence is adapted through, and every occurrence's own instance-level
/// overrides. <see cref="SourcePhraseId"/> is "the first approved performance" by default (Section 12), but
/// nothing prevents a future step from reassigning it -- Section 3: "a user may reassign the canonical
/// source" (not implemented here; this step only fixes one source per template and refreshes it in place).
/// </summary>
/// <param name="Id">Stable identity for this one template -- preserved across a <see cref="IChoreographyTemplatePlanner.DefineTemplateAsync"/> refresh for the same <see cref="MotifGroupId"/>/<see cref="SourcePhraseId"/> pair.</param>
/// <param name="MotifGroupId">The owning <see cref="MotifGroupCandidate.Id"/> this template's occurrences are drawn from.</param>
/// <param name="SourcePhraseId">The phrase this template's own <see cref="Effects"/> were captured from -- the chosen "master" occurrence.</param>
/// <param name="Mask">The inheritance mask every non-overridden effect is adapted through onto a target phrase.</param>
/// <param name="Effects">This template's own captured effects, relative to <see cref="SourcePhraseId"/>'s own start. Re-captured (same <see cref="ChoreographyTemplateEffect.SourceEffectId"/> keys where the originating effect still exists) every time the template is refreshed -- "editing the master" is simply calling <see cref="IChoreographyTemplatePlanner.DefineTemplateAsync"/> again after the source document's own effects changed.</param>
/// <param name="Instances">Every occurrence that has at least one instance-level override recorded. A target phrase with no entry here still inherits the template in full.</param>
public sealed record ChoreographyTemplate(
    Guid Id,
    Guid MotifGroupId,
    Guid SourcePhraseId,
    ChoreographyInheritanceMask Mask,
    IReadOnlyList<ChoreographyTemplateEffect> Effects,
    IReadOnlyList<ChoreographyInstance> Instances);
