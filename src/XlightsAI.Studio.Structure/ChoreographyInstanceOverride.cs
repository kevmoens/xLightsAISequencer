using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// An explicit, human-authored replacement for the propagated effect that would otherwise be inherited
/// from one specific <see cref="ChoreographyTemplateEffect.SourceEffectId"/> at one specific
/// <see cref="ChoreographyInstance.TargetPhraseId"/> -- Step 057's own required acceptance, verbatim: "Edit
/// master/motif occurrence without surprising cross-section changes." An override always wins over the
/// template's own inherited value for that one occurrence, regardless of <see cref="ChoreographyInheritanceMask"/>,
/// and never affects the template itself or any other occurrence's own preview.
/// </summary>
/// <param name="SourceEffectId">Which template effect slot this override replaces. If the template is later refreshed and no longer captures an effect with this id, this override is still honored as a standalone, instance-only addition -- removing a human's explicit choice without consent would itself be a "surprising cross-section change."</param>
/// <param name="Target">The model/group this overriding effect targets.</param>
/// <param name="Layer">The effect layer this overriding effect occupies.</param>
/// <param name="StartTicks">This overriding effect's own absolute start tick within the target phrase.</param>
/// <param name="EndTicks">This overriding effect's own absolute end tick (half-open).</param>
/// <param name="RecipeId">The recipe identity this overriding effect uses.</param>
/// <param name="RecipeVersion">The recipe version this overriding effect uses.</param>
/// <param name="Parameters">This overriding effect's own named recipe inputs, used verbatim -- never filtered by <see cref="ChoreographyInheritanceMask.Palette"/> (an explicit human edit is not subject to the mask).</param>
public sealed record ChoreographyInstanceOverride(
    Guid SourceEffectId,
    ModelId Target,
    int Layer,
    long StartTicks,
    long EndTicks,
    string RecipeId,
    string RecipeVersion,
    IReadOnlyDictionary<string, string> Parameters);
