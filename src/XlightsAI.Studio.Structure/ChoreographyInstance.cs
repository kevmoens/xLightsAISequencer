namespace XlightsAI.Studio.Structure;

/// <summary>
/// One occurrence of a <see cref="ChoreographyTemplate"/> at a specific <see cref="TargetPhraseId"/> --
/// the "instance" half of Step 057's own title, verbatim: "parent/instance pattern inheritance with
/// adjustable mask and transform." A target phrase with no <see cref="ChoreographyInstance"/> entry at all
/// still inherits the template in full (pure inheritance, no overrides needed); an entry only needs to
/// exist once a human has overridden at least one effect for that one occurrence.
/// </summary>
/// <param name="Id">Stable identity for this one instance record.</param>
/// <param name="TemplateId">The owning <see cref="ChoreographyTemplate.Id"/>.</param>
/// <param name="TargetPhraseId">Which phrase in the same motif group this occurrence adapts the template onto. May equal the template's own <see cref="ChoreographyTemplate.SourcePhraseId"/> -- editing one specific occurrence of the master phrase itself is a distinct act from editing the template's own captured structure (Step 057's own required acceptance, verbatim: "Edit master/motif occurrence...").</param>
/// <param name="Overrides">Explicit, instance-only effect replacements/additions, keyed by <see cref="ChoreographyInstanceOverride.SourceEffectId"/>. Never affects the template or any other instance's own preview.</param>
public sealed record ChoreographyInstance(
    Guid Id,
    Guid TemplateId,
    Guid TargetPhraseId,
    IReadOnlyList<ChoreographyInstanceOverride> Overrides);
