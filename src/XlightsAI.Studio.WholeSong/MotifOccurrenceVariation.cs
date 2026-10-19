using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.WholeSong;

/// <summary>
/// One occurrence's own scheduled result -- "display inherited vs instance-specific effects" (Step 092's own
/// implementation guidance, verbatim). <see cref="InheritedSourceEffectIds"/>/<see cref="InstanceOverrideSourceEffectIds"/>
/// classify the template's own effect slots for this one occurrence at the source-slot level (the same
/// granularity <c>ChoreographyTemplate.Effects</c>/<c>ChoreographyInstance.Overrides</c> are themselves keyed
/// by); a slot named here may still be absent from <see cref="Effects"/> if
/// <c>IChoreographyTemplatePlanner.PreviewInstance</c> itself excluded it (a locked destination conflict, or a
/// <c>ChoreographyTimingPolicy.Copy</c> window that does not fit) -- that exclusion is named verbatim in
/// <see cref="ExcludedReasons"/>, never silently dropped from view.
/// </summary>
/// <param name="TemplateId">The template this occurrence adapts.</param>
/// <param name="TargetPhraseId">The phrase this occurrence targets.</param>
/// <param name="IsClimax">Whether <see cref="IMotifVariationScheduler.Schedule"/>'s own climax-scheduling rule selected this occurrence, among every occurrence sharing the same <see cref="TemplateId"/> in the same request set, as the one intentional-lift occurrence -- the single highest <see cref="MotifOccurrenceRequest.SectionEnergy"/> in that group (first-in-request-order on an exact tie).</param>
/// <param name="IntensityMultiplier">The exact, bounded multiplier <see cref="Effects"/>'s own <c>"Intensity."</c>-prefixed parameters were scaled by -- see <see cref="MotifVariationScheduler"/> for the full, provable "climax always strictly exceeds every non-climax occurrence of the same template" guarantee.</param>
/// <param name="Effects">The real, adapted, intensity-scaled effect list this occurrence would receive -- never itself a mutation of any <see cref="SequenceDocument"/> (the same "preview, not a direct write" convention <c>ChoreographyPreviewResult</c> already established).</param>
/// <param name="InheritedSourceEffectIds">Every template-effect slot (<c>ChoreographyTemplateEffect.SourceEffectId</c>) this occurrence inherits by semantic mask propagation, not an explicit human override.</param>
/// <param name="InstanceOverrideSourceEffectIds">Every template-effect slot this occurrence instead replaces with an explicit, human-authored <c>ChoreographyInstanceOverride</c> -- never intensity-scaled (an explicit human edit is not subject to this step's own automatic transform, the same "overrides always win" rule <c>ChoreographyTemplatePlanner</c> already established for its own mask).</param>
/// <param name="ExcludedReasons">Carried through unchanged from the underlying <c>ChoreographyPreviewResult.ExcludedReasons</c>.</param>
public sealed record MotifOccurrenceVariation(
    Guid TemplateId,
    Guid TargetPhraseId,
    bool IsClimax,
    double IntensityMultiplier,
    IReadOnlyList<EffectInstance> Effects,
    IReadOnlyList<Guid> InheritedSourceEffectIds,
    IReadOnlyList<Guid> InstanceOverrideSourceEffectIds,
    IReadOnlyList<string> ExcludedReasons);
