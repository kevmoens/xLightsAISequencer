using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Choreography;

/// <summary>Names a Look's own named parameter-curve binding (architecture §4's <c>"curves"</c>: <c>{"ColorWash_Cycles":{"type":"Timing Track Fade Proportional","track":"Beats"}}</c>).</summary>
/// <param name="Type">The xLights value-curve type name (e.g. <c>"Timing Track Fade Proportional"</c>, <c>"Music"</c>) -- resolved against the real vocabulary at compile time, never invented.</param>
/// <param name="Track">The timing track (for a <c>Timing Track *</c> type) or audio track (for a <c>Music</c> type) this curve is bound to, or <see langword="null"/> for a type that needs neither.</param>
public sealed record LookCurveBinding(string Type, string? Track = null);

/// <summary>A declared in/out transition (architecture §4's <c>"transitionIn"</c>). <paramref name="Duration"/> is a note value, never a literal second count -- "fades expressed in note values, never milliseconds," architecture §7.</summary>
public sealed record LookTransition(string Type, NoteValue Duration, bool Reverse = false);

/// <summary>
/// Architecture §4: one layer of a <see cref="Look"/> -- one effect bound to one prop group at one
/// z-order, with its own role, blend and timing.
/// </summary>
/// <param name="Z">Layer/z-order within the Look -- xLights' own "layer 1 is the first EffectLayer in document order and is the topmost" convention (architecture §4).</param>
/// <param name="Props">A group/subject reference this layer targets (e.g. <c>"group:MiniTrees"</c>, or a bare model/group name) -- resolved against the real <c>DisplayGraph</c> at compile time.</param>
/// <param name="Effect">The xLights effect name this layer uses -- resolved against a registered <c>IEffectRecipe</c> (generated from the real vocabulary) at compile time.</param>
/// <param name="Role">One of <c>"subject"</c>, <c>"support"</c>, <c>"secondary"</c> (architecture §4: "every Look names one subject; everything else is support or secondary").</param>
/// <param name="Params">Declared plain parameter overrides, keyed by the real <c>SettingKey</c> -- validated against the vocabulary at compile time, never invented.</param>
/// <param name="Blend">The xLights blend mode name (e.g. <c>"Normal"</c>, <c>"Bright"</c>, <c>"Additive"</c>, <c>"Max"</c>) -- carried as IR only; see <c>XsqExporter</c>'s own disclosed gap (no verified <c>LayerMethod</c> choice list exists yet).</param>
/// <param name="Timescale">How this layer's own Mode B discrete-emission cadence (when Mode A is unavailable) is chosen -- e.g. <c>"bar"</c>, <c>"beat"</c>, <c>"phrase"</c>.</param>
/// <param name="Duty">Declared duty-cycle target for this layer in <c>[0,1]</c> -- cross-checked against <see cref="DutyCycleTable"/>'s own per-effect-family correction, not simply trusted.</param>
/// <param name="Curves">Named parameter-curve bindings (architecture §4's <c>"curves"</c>), keyed by the plain parameter's <c>SettingKey</c>.</param>
/// <param name="TransitionIn">A declared in-transition, or <see langword="null"/> for a hard cut.</param>
public sealed record LookLayer(
    int Z,
    string Props,
    string Effect,
    string Role,
    IReadOnlyDictionary<string, string>? Params,
    string Blend,
    string? Timescale,
    double Duty,
    IReadOnlyDictionary<string, LookCurveBinding>? Curves = null,
    LookTransition? TransitionIn = null)
{
    public const string RoleSubject = "subject";
    public const string RoleSupport = "support";
    public const string RoleSecondary = "secondary";

    public IReadOnlyDictionary<string, string> ParamsOrEmpty => Params ?? EmptyParams;

    public IReadOnlyDictionary<string, LookCurveBinding> CurvesOrEmpty => Curves ?? EmptyCurves;

    private static readonly IReadOnlyDictionary<string, string> EmptyParams = new Dictionary<string, string>();
    private static readonly IReadOnlyDictionary<string, LookCurveBinding> EmptyCurves = new Dictionary<string, LookCurveBinding>();
}

/// <summary>
/// Architecture §4: a named, reusable, complete composition -- "what every prop is doing at once... the
/// unit lighting designers actually think in." The exact JSON shape architecture §4's own worked example
/// (<c>golden-hold</c>) shows, carried 1:1 into C#.
/// </summary>
/// <param name="Id">Stable, unique identity within a <see cref="LookLibrary"/>.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="Intent">One-sentence declared creative intent -- what a human reviewing the Show Bible reads to judge it, and what <see cref="ChoreographyLegalityValidator"/>'s declared-darkness rule checks against.</param>
/// <param name="EnergyCeiling">This Look's own energy ceiling in <c>[0,1]</c> -- never exceeded when compiled against a section whose own declared ceiling (architecture §10's structural arc) is lower.</param>
/// <param name="Motion">One of <c>"hold"</c>, <c>"chase"</c>, <c>"build"</c>, <c>"pop"</c>, <c>"decay"</c> -- the always-moving rule's own filler-pool/cadence selection consults this.</param>
/// <param name="PaletteRef">A named reference into the Show Bible's palette set -- resolved to a real <c>ColorPaletteDefinition</c> at compile time.</param>
/// <param name="Subject">The one prop/group this Look's subject layer targets -- architecture §4: "every Look names one subject."</param>
/// <param name="Layers">This Look's own layers, in declared z-order.</param>
/// <param name="PropDelays">Mass-tier-keyed stagger offsets (e.g. <c>{"mass":"+0ms","mid":"+60ms","accent":"+120ms"}</c>) -- architecture §4's <c>propDelays</c>, resolved by <see cref="PropDelayResolver"/>.</param>
/// <param name="Uniqueness">A reservation tag (e.g. <c>"reserved:peak"</c>) -- <see cref="ChoreographyLegalityValidator"/>'s own "a Look reserved for a peak binds to exactly one section" rule enforces this, never fabricated as satisfied.</param>
/// <param name="Variations">Declared variation names this Look supports (e.g. <c>"escalate"</c>, <c>"invert"</c>) -- consumed by a later phase's variation scheduler; carried here as declared capability, not yet applied by Phase 4's own compiler.</param>
/// <param name="DeclaredDarkness">
/// Whether this Look's own near-black/negative-space behavior is a deliberate creative choice rather
/// than an incidental consequence of a low energy ceiling -- architecture §7: "it must be declared in
/// the Look, not fall out of a low RMS value." <see cref="ChoreographyLegalityValidator"/> flags a
/// compiled section whose rendered duty/coverage reads as near-black without this flag set.
/// </param>
/// <param name="AllowedSectionKinds">Which <see cref="SectionKind"/>s this Look may be assigned to (architecture §5: "each tagged with which section kinds may use it"); <see langword="null"/>/empty means unrestricted.</param>
/// <param name="TemplateRef">
/// Build-plan Phase 10 §6b task 4: the id of a seed <c>MultiLayerTemplate</c> (from
/// <c>XlightsAI.Studio.Knowledge.MultiLayerTemplateCatalog</c>) this Look's own layer composition was
/// directly modeled on, or <see langword="null"/> when this Look was not drawn from any named seed
/// template. The Director declares this itself (it is told the catalog in its own prompt) rather than
/// it being inferred after the fact -- so the Shell can show a user watching a generation run which seed
/// pattern actually shaped a given Look, instead of that mechanism being invisible.
/// </param>
public sealed record Look(
    string Id,
    string Name,
    string Intent,
    double EnergyCeiling,
    string Motion,
    string PaletteRef,
    string Subject,
    IReadOnlyList<LookLayer> Layers,
    IReadOnlyDictionary<string, string>? PropDelays = null,
    string? Uniqueness = null,
    IReadOnlyList<string>? Variations = null,
    bool DeclaredDarkness = false,
    IReadOnlyList<SectionKind>? AllowedSectionKinds = null,
    string? TemplateRef = null)
{
    public IReadOnlyDictionary<string, string> PropDelaysOrEmpty => PropDelays ?? EmptyDelays;

    public IReadOnlyList<string> VariationsOrEmpty => Variations ?? [];

    public IReadOnlyList<SectionKind> AllowedSectionKindsOrEmpty => AllowedSectionKinds ?? [];

    private static readonly IReadOnlyDictionary<string, string> EmptyDelays = new Dictionary<string, string>();
}

/// <summary>A collection of hand-authored (or, in a later phase, Director-produced) <see cref="Look"/>s a song's sections are compiled against.</summary>
public sealed record LookLibrary(IReadOnlyList<Look> Looks)
{
    public Look? Find(string id) => Looks.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.Ordinal));
}
