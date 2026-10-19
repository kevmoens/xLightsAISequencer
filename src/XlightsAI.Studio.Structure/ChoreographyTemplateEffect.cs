using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// One effect captured from the template's own source phrase's already-approved
/// <see cref="XlightsAI.Studio.Xlights.SequenceDocument"/> at the moment <see cref="IChoreographyTemplatePlanner.DefineTemplateAsync"/>
/// (re-)captured it, expressed relative to that source phrase's own start so it can be re-timed onto any
/// target phrase in the same motif group. <see cref="SourceEffectId"/> is the stable key every
/// <see cref="ChoreographyInstanceOverride"/> addresses "the propagated effect that came from this specific
/// source effect" by.
/// </summary>
/// <param name="SourceEffectId">The originating <see cref="XlightsAI.Studio.Xlights.EffectInstance.Id"/> in the source phrase -- stable across a template refresh as long as the same effect still exists in the source document (effect identity is never reassigned by an edit, only its fields mutate).</param>
/// <param name="Target">The model/group this effect targets -- carried forward unchanged onto every instance (a prop's identity is global, not phrase-local).</param>
/// <param name="Layer">The effect layer this effect occupies in the source document.</param>
/// <param name="RelativeStartTicks">This effect's own start, relative to the source phrase's own start tick.</param>
/// <param name="RelativeEndTicks">This effect's own end (half-open), relative to the source phrase's own start tick.</param>
/// <param name="RecipeId">The recipe identity this effect was produced from (carried forward unchanged -- Section 3: "Store recipe identity independent of final xLights XML settings representation").</param>
/// <param name="RecipeVersion">The recipe version this effect was produced from.</param>
/// <param name="Parameters">This effect's own named recipe inputs. Keys prefixed <c>"Palette."</c> are this recipe's own declared convention for palette-aspect parameters (<see cref="ChoreographyInheritanceMask.Palette"/>); every other key is a motion-aspect parameter.</param>
/// <param name="IsRhythmLayer">Whether <see cref="Layer"/> was declared a rhythm/drum layer at template-definition time (<see cref="ChoreographyTemplateContext"/>'s own caller-supplied classification) -- gates <see cref="ChoreographyInheritanceMask.RhythmLayer"/>.</param>
public sealed record ChoreographyTemplateEffect(
    Guid SourceEffectId,
    ModelId Target,
    int Layer,
    long RelativeStartTicks,
    long RelativeEndTicks,
    string RecipeId,
    string RecipeVersion,
    IReadOnlyDictionary<string, string> Parameters,
    bool IsRhythmLayer);
