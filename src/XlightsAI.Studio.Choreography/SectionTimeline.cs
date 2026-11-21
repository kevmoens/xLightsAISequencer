namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 28a. Which real, already-named timing mark a <see cref="SectionCue"/> is anchored to
/// -- NEVER a free-floating invented timestamp. The Choreographer picks from a concrete enumerated list
/// the prompt gives it (<see cref="SectionTimingMarks.Resolve"/>, rendered by
/// <c>XlightsAI.Studio.Director.SectionScriptPromptBuilder</c>'s own "Real timing marks" block);
/// <see cref="SectionTimelineValidator"/> rejects any AnchorId that doesn't match one of the marks
/// actually offered for this section.
/// </summary>
public static class AnchorKind
{
    public const string Beat = "Beat";
    public const string Bar = "Bar";
    public const string Transient = "Transient";
    public const string VocalNote = "VocalNote";
    public const string LeadInstrumentWindowStart = "LeadInstrumentWindowStart";
    public const string PhraseStart = "PhraseStart";
    public const string SectionStart = "SectionStart"; // the one allowed "no specific mark needed" anchor
}

/// <summary>
/// Build-plan Phase 28a. Coarse, enforceable brightness/density tier -- NOT a raw float. Four tiers only,
/// matching the four tiers <see cref="EmphasisContrastPlanner"/> already reasons in internally, so this
/// finally gives the LLM the SAME vocabulary the mechanical contrast-fallback pass uses, instead of two
/// disconnected brightness models existing side by side.
/// </summary>
public static class IntensityTier
{
    public const string Off = "Off";
    public const string Dim = "Dim";
    public const string Bright = "Bright";
    public const string Full = "Full";
}

public static class CueKind
{
    public const string Hold = "hold";
    public const string Accent = "accent";
    public const string Takeover = "takeover";
}

/// <summary>
/// Build-plan Phase 28a. One real, timed, model-level decision. Replaces "pick one LookId for the whole
/// section" with "here is what happens, where, and when" -- a sparse list, not a keyframe track: a prop
/// with no cue simply continues whatever its last cue declared (or the base Look's own default if it has
/// no cue at all).
/// </summary>
/// <param name="AnchorId">
/// The id of a real timing mark offered in this section's own prompt (e.g. "transient-2",
/// "vocalnote-5", "beat-7") -- resolved server-side to a real tick value via
/// <see cref="SectionTimelineCompiler.ResolveAnchor"/>. Never a value the Choreographer invented.
/// </param>
/// <param name="Props">A real prop/group reference, same convention as <see cref="LookLayer.Props"/>.</param>
/// <param name="Effect">A real registered effect name, validated the same way <see cref="LookLayer.Effect"/> is.</param>
/// <param name="Intensity">One of <see cref="IntensityTier"/> -- the enforceable brightness/density step.</param>
/// <param name="Role">subject|support|secondary|foundation -- same vocabulary as <see cref="LookLayer.Role"/>.</param>
/// <param name="HoldUntilAnchorId">
/// Optional -- which later real anchor this cue holds through before the NEXT cue on the same Props
/// takes over. Omitted means "until superseded by the next cue naming this same Props, or section end."
/// </param>
/// <param name="HandoffFromProps">
/// Optional -- names the prop/group this cue's motion/emphasis is explicitly "handing off" from, e.g. a
/// sound/sweep moving from "group:Arches Left" to "group:Arches Right". When set,
/// <see cref="SectionTimelineCompiler"/> builds a real cross-fade/stagger between the two real props
/// timed to this cue's own anchor, rather than two independent, disconnected effects.
/// </param>
/// <param name="Annotation">
/// Grounded, one-sentence why -- same grounding discipline as <see cref="LookLayer.Annotation"/>, but
/// THIS ONE IS CONSUMED: written into the compiled <c>EffectInstance.SourceAnnotation</c>. Never a guess;
/// omit rather than invent.
/// </param>
/// <param name="Compound">
/// Build-plan Phase 30 step 30.2 ("hit-motion-compound"): opt-in, per-cue -- never a blanket rule, same
/// discipline as every other Phase 30 addition. Only takes effect for a <see cref="CueKind.Accent"/> cue
/// (ignored otherwise, same silent-no-op convention <see cref="HandoffFromProps"/> already uses for a
/// non-accent cue). When <see langword="true"/>, <see cref="SectionTimelineCompiler"/> pairs this cue's
/// own base hit effect with a second, independent movement-overlay layer
/// (<see cref="HitMotionCompound.TryBuild"/>) spanning the identical resolved cue window, instead of the
/// flat single-layer hit every other accent cue still gets.
/// </param>
public sealed record SectionCue(
    string AnchorId,
    string Props,
    string Effect,
    string Intensity,
    string Role,
    string? HoldUntilAnchorId = null,
    string? HandoffFromProps = null,
    string? Annotation = null,
    string Kind = CueKind.Accent,
    IReadOnlyList<string>? Targets = null,
    string? PaletteSlot = null,
    bool Compound = false)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? TraceId { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsMeasuredLitSpan { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> TargetReferences => Targets is { Count: > 0 } ? Targets : [Props];
}

/// <summary>
/// Build-plan Phase 28a: the per-section output, additive alongside the existing <see cref="SectionScript"/>.
/// <see cref="LookId"/> is kept -- it still anchors palette/energy-ceiling/motion-style discipline and
/// supplies the DEFAULT composition for any prop/moment this timeline doesn't explicitly cue.
/// <see cref="Cues"/> is the new, sparse, time-anchored override layer.
///
/// Deliberately carries every one of <see cref="SectionScript"/>'s own fields (<see cref="Variation"/>,
/// <see cref="PropBindings"/>, <see cref="PhraseVariations"/>) in addition to <see cref="Cues"/> --
/// migration-plan §6's own "a strict superset" claim (an old-shape response with no "cues" key
/// deserializes identically to today) only actually holds if this type's JSON shape is a real superset of
/// <see cref="SectionScript"/>'s, not just of the new fields the Phase 28 doc's own worked example
/// enumerates. Without these three fields, a deserializer configured with
/// <c>JsonUnmappedMemberHandling.Disallow</c> (the convention <see cref="XlightsAI.Studio.Director.SectionScriptBatchGenerator"/>/
/// <see cref="XlightsAI.Studio.Director.SectionScriptGenerator"/> already use) would reject every
/// pre-existing, still-perfectly-valid flat response the moment the deserialization target switched from
/// <see cref="SectionScript"/> to this type -- the exact backward-compatibility break §6 explicitly rules
/// out. Resolved here rather than guessed at the call site: a single additive type is simpler than two
/// parallel deserialization paths.
/// </summary>
public sealed record SectionTimeline(
    Guid SectionId,
    string LookId,
    string? Variation = null,
    IReadOnlyList<SectionEventBinding>? PropBindings = null,
    IReadOnlyList<SectionPhraseVariation>? PhraseVariations = null,
    IReadOnlyList<SectionCue>? Cues = null,
    IReadOnlyList<string>? FillerEffects = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? FillerEffectsByRole = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SectionCue> CuesOrEmpty => Cues ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SectionEventBinding> PropBindingsOrEmpty => PropBindings ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<SectionPhraseVariation> PhraseVariationsOrEmpty => PhraseVariations ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> FillerEffectsOrEmpty => FillerEffects ?? [];

    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FillerEffectsByRoleOrEmpty => FillerEffectsByRole ?? new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// Build-plan Phase 28a §6's own backward-compatibility seam: the flat-<see cref="SectionScript"/>
    /// projection of this timeline, dropping only <see cref="Cues"/> -- used by every caller that has not
    /// opted into <c>EnableSectionTimelines</c>, or whose own response declared no real cues, so today's
    /// <see cref="SectionScriptValidator"/>/<see cref="LookCompiler"/> path runs completely unchanged.
    /// </summary>
    public SectionScript ToSectionScript() => new(SectionId, LookId, Variation, PropBindings, PhraseVariations, FillerEffects, FillerEffectsByRole);

    /// <summary>The inverse of <see cref="ToSectionScript"/> -- every pre-existing <see cref="SectionScript"/> construction site/persisted file round-trips into a <see cref="SectionTimeline"/> with no <see cref="Cues"/>.</summary>
    public static SectionTimeline FromSectionScript(SectionScript script) => new(
        script.SectionId, script.LookId, script.Variation, script.PropBindings, script.PhraseVariations, null, script.FillerEffects, script.FillerEffectsByRole);
}
