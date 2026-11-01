namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 6 task 1: one layer-local override within a <see cref="SectionScript"/> -- lets a
/// section point one of its assigned Look's layers at a different real prop/group than that Look's own
/// default, without inventing a brand-new Look just to change a target (e.g. the same "golden-hold"
/// Look's subject layer can carry the melody on <c>Tree</c> in one verse and <c>group:Arches</c> in
/// another, if the Show Bible left that choice to the Choreographer). Keyed by <see cref="LookLayer.Z"/>
/// -- a Look's layers already carry a stable z-order (architecture §4), so no separate layer-identity
/// concept is introduced here. Lives alongside <see cref="Look"/> itself (not in
/// <c>XlightsAI.Studio.Director</c>, despite being an L3/Choreographer-role output) because
/// <see cref="SectionScriptCompiler"/> -- a pure, I/O-free compiler, same as <see cref="LookCompiler"/> --
/// must consume it directly, and Director already depends on Choreography, never the reverse.
/// </summary>
/// <param name="LayerZ">Which of the referenced Look's layers (by <see cref="LookLayer.Z"/>) this binding overrides.</param>
/// <param name="Props">The real prop/group reference (e.g. <c>"group:MiniTrees"</c>, a bare model name) this section uses for that layer instead of the Look's own declared <see cref="LookLayer.Props"/>.</param>
public sealed record SectionEventBinding(int LayerZ, string Props);

/// <summary>
/// Build-plan Phase 10 §2e/§3e: one phrase-scoped staging decision within a section -- "phrase 2 of 4: add
/// layer X" / "phrase 4 of 4: escalate toward the next section." <see cref="PhraseIndex"/> is 1-based,
/// counting this section's own phrases in <c>XlightsAI.Studio.Structure.PhraseCandidate.StartBarIndex</c>
/// order (phrase 1 is the section's own first phrase) -- the same 1-based "repeat N of M" convention this
/// project already uses elsewhere (<c>XlightsAI.Studio.Director.SectionScriptPromptBuilder</c>'s own
/// occurrenceIndex). A phrase this section has that is NOT named by any one
/// <see cref="SectionScript.PhraseVariations"/> entry compiles exactly like today -- the section's own base
/// Look (with any section-wide <see cref="SectionScript.PropBindings"/>, but no phrase-local override) over
/// just that phrase's own bar range -- so declaring one or two staged phrases never forces the Choreographer
/// to also describe every other phrase in the section.
/// </summary>
/// <param name="PhraseIndex">1-based position of the phrase this entry scopes to, among this section's own phrases, ordered by their own start bar.</param>
/// <param name="ActiveLayerZs">
/// The full set of the referenced Look's own layer <see cref="LookLayer.Z"/> values active for this phrase
/// -- always complete, never an incremental add relative to another phrase, so "phrase 1 establishes the
/// Look" can declare a smaller subset and "phrase 3 adds layer X" can declare that same subset plus one
/// more -- or <see langword="null"/> to use every layer of the Look (the same set a non-phrase-scoped
/// section already gets) for this phrase specifically.
/// </param>
/// <param name="PropBindings">Per-layer prop/group rebindings that apply ONLY for this phrase's own window (e.g. a palette-accent swap for one phrase) -- overrides, for just this phrase, any section-wide <see cref="SectionScript.PropBindings"/> declared for the same <see cref="SectionEventBinding.LayerZ"/>.</param>
public sealed record SectionPhraseVariation(
    int PhraseIndex,
    IReadOnlyList<int>? ActiveLayerZs = null,
    IReadOnlyList<SectionEventBinding>? PropBindings = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<int> ActiveLayerZsOrEmpty => ActiveLayerZs ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SectionEventBinding> PropBindingsOrEmpty => PropBindings ?? [];
}

/// <summary>
/// Build-plan Phase 6 task 1: "minimal per-section output -- which Look, which variation, how events bind
/// to specific props/groups for that section -- kept flat per the structured-decoding reasoning tax
/// (architecture §2)." This is L3's entire output shape: it never re-describes a Look's own layers,
/// parameters, palette or timing -- it only *selects* a Look already declared in the Show Bible and
/// *rebinds* specific layers to this section's own real props, which is the one decision a Look's author
/// (the Director, working from the whole song) cannot make for every individual section in advance.
/// </summary>
/// <param name="SectionId">Which classified section (<c>XlightsAI.Studio.Structure.SectionLabelCandidate.Id</c>) this script is for.</param>
/// <param name="LookId">Which <see cref="Look"/> (by <see cref="Look.Id"/>) from the Show Bible's own Look library this section uses.</param>
/// <param name="Variation">
/// One of the referenced Look's own declared <see cref="Look.Variations"/> names, or <see langword="null"/>
/// for the Look's plain form. Carried here as a declared creative decision, but -- same disclosed
/// limitation as <see cref="Look.Variations"/> itself -- <see cref="SectionScriptCompiler"/> does not yet
/// apply a variation's own effect; a later phase's variation scheduler is what gives this field teeth.
/// </param>
/// <param name="PropBindings">Per-layer prop/group rebindings for this section, or <see langword="null"/>/empty to use the Look's own layers unchanged.</param>
/// <param name="PhraseVariations">
/// Build-plan Phase 10 §2e/§3e: up to a small number (2-3) of phrase-scoped staging variations within this
/// section, or <see langword="null"/>/empty to fall back to today's one-Look-for-the-whole-section
/// behavior -- additive, never required. See <see cref="SectionPhraseVariation"/>.
/// </param>
/// <param name="FillerEffects">
/// Build-plan Phase 15 §2c/§2d: this section's own flat, section-wide rotating filler pool (real xLights
/// effect names, e.g. <c>"On"</c>/<c>"Bars"</c>) for the always-moving rule's negative-space gaps on any
/// prop this section does not otherwise animate -- required in practice: <see cref="SectionScriptValidator"/>
/// treats a null/empty value, or fewer than <see cref="FillerPool.MinimumDistinctStates"/> distinct names,
/// as a retry-worthy violation, the same discipline an invalid <see cref="LookId"/> already goes through.
/// Kept nullable here only so <c>System.Text.Json</c> deserialization of a model response missing the
/// field never throws before that validation ever runs. Replaces the old whole-song, pre-AI hardcoded
/// rotation (<c>AiChoreographyWholeSongGenerator.BuildFillerPool</c>, now deleted) -- a real bug fixed by
/// this phase: that rotation had no visibility into the user's own custom prompt, so "do not use the
/// butterfly effect" was honored by the Director's own Show Bible but not by filler.
/// </param>
/// <param name="FillerEffectsByRole">
/// Build-plan Phase 15 §2c: an optional, additive narrowing of <see cref="FillerEffects"/> per
/// <c>XlightsAI.Studio.Director.BandEnergyGroupRoleMapper.BandRole</c> -- keyed by the plain strings
/// <c>"Foundation"</c>/<c>"Subject"</c>/<c>"Accent"</c> (kept as strings, not that enum, because this
/// project's compiler -- <see cref="SectionScriptCompiler"/> -- lives in <c>Choreography</c>, which must
/// never depend on <c>Director</c>; role-name-to-enum resolution happens only at the
/// <c>Director</c>-layer integration point). When a filler target classifies into a role with an entry
/// here, that list is used for it in this section instead of the flat <see cref="FillerEffects"/>. Every
/// present role's own list is held to the same ≥<see cref="FillerPool.MinimumDistinctStates"/>-distinct
/// rule as the flat list -- a narrower list is still a real rotating pool, never a relaxed one.
/// </param>
public sealed record SectionScript(
    Guid SectionId,
    string LookId,
    string? Variation = null,
    IReadOnlyList<SectionEventBinding>? PropBindings = null,
    IReadOnlyList<SectionPhraseVariation>? PhraseVariations = null,
    IReadOnlyList<string>? FillerEffects = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FillerEffectsByRole = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SectionEventBinding> PropBindingsOrEmpty => PropBindings ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SectionPhraseVariation> PhraseVariationsOrEmpty => PhraseVariations ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> FillerEffectsOrEmpty => FillerEffects ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FillerEffectsByRoleOrEmpty => FillerEffectsByRole ?? new Dictionary<string, IReadOnlyList<string>>();
}
