namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 057's own narrowing of Section 3's proposed free-form <c>string InheritanceMask</c> on
/// <c>MotifLink</c>/<c>MotifLinkCandidate</c> into an explicit, checkable set of aspects -- Section 12's own
/// worked example, verbatim: <c>{ motion: true, timing: retime, palette: false, drum_layer: false }</c>. A
/// disclosed, deliberate narrowing (a typed record instead of an opaque string) so <see cref="IChoreographyTemplatePlanner"/>
/// can act on the mask mechanically rather than parsing free text.
/// </summary>
/// <param name="Motion">Whether this template's own captured gesture structure (<see cref="ChoreographyTemplateEffect.RecipeId"/>/<see cref="ChoreographyTemplateEffect.Parameters"/>) is propagated at all. When <see langword="false"/>, no inherited effect is proposed for any template effect not already covered by an explicit <see cref="ChoreographyInstanceOverride"/> -- there is no gesture left to adapt.</param>
/// <param name="Timing">How a propagated effect's tick window is derived from the target phrase.</param>
/// <param name="Palette">Whether palette-tagged parameters (keys prefixed <c>"Palette."</c>, by this recipe's own declared convention) are copied into a propagated effect. When <see langword="false"/>, those keys are stripped so the destination keeps whatever palette it already has.</param>
/// <param name="RhythmLayer">Whether template effects on a declared rhythm/drum layer (<see cref="ChoreographyTemplateEffect.IsRhythmLayer"/>) are propagated at all. When <see langword="false"/>, Section 12's own example is satisfied verbatim: "leaves destination-specific D untouched" -- the destination's own existing rhythm-layer effects are never proposed over.</param>
public sealed record ChoreographyInheritanceMask(
    bool Motion,
    ChoreographyTimingPolicy Timing,
    bool Palette,
    bool RhythmLayer);
