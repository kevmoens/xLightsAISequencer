namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// One layer within a <see cref="MultiLayerTemplate"/> -- deliberately mirrors
/// <c>XlightsAI.Studio.Choreography.LookLayer</c>'s own shape (<c>Role</c>, <c>Effect</c>, a duty-cycle
/// range, a cadence/timescale hint) so a template layer can become a real <c>LookLayer</c> directly,
/// without a translation step. Lives in <c>Knowledge</c> rather than <c>Choreography</c> (which this
/// project cannot reference without a cyclic project reference -- <c>Choreography</c> does not
/// reference <c>Knowledge</c>, but nothing here needs <c>LookLayer</c>'s compiled-against-a-recipe
/// identity either; a template is deliberately looser/pre-compile than a real <see cref="LookLayer"/>).
/// </summary>
/// <param name="Role">One of <c>"subject"</c>, <c>"support"</c>, <c>"secondary"</c> -- the same three values <c>LookLayer.Role</c> accepts.</param>
/// <param name="Effect">A real xLights effect name (e.g. <c>"SingleStrand"</c>, <c>"Shockwave"</c>) -- cross-checked against the real vocabulary when this template is consulted, never trusted blind.</param>
/// <param name="DutyMin">Lower bound of this layer's approximate duty-cycle target in <c>[0,1]</c>, observed across the source research extracts.</param>
/// <param name="DutyMax">Upper bound of this layer's approximate duty-cycle target in <c>[0,1]</c>.</param>
/// <param name="Cadence">A short, human-readable cadence/timescale hint (e.g. <c>"beat"</c>, <c>"1-2 beats"</c>, <c>"phrase"</c>, <c>"every 5-6 seconds"</c>) -- deliberately prose, not a literal <c>Timescale</c> enum value, since the research this is derived from describes cadence more precisely than a single discrete bucket.</param>
/// <param name="Notes">A short, palette-free, song-free description of what this layer actually does and why, carried into the Director prompt verbatim.</param>
public sealed record MultiLayerTemplateLayer(
    string Role,
    string Effect,
    double DutyMin,
    double DutyMax,
    string Cadence,
    string Notes);

/// <summary>
/// A generic, palette-free/song-free multi-layer composition pattern for a dense, high-resolution prop
/// (Tree/Matrixes/Mega Tree) -- build-plan Phase 10 §6b task 1. Derived by manual curation (an
/// explicitly-approved first pass; see <c>docs/research/multilayer-template-exemplars/README.md</c>)
/// from a real scan of the user's own sequence library, with every literal color/song-specific timing
/// stripped out -- only the structural shape (layer roles, effect families, approximate duty-cycle/
/// cadence relationships) survives, which is exactly what that research document's own licensing note
/// requires before anything is shipped in the app build.
/// </summary>
/// <param name="Id">Stable, unique identity within a <see cref="MultiLayerTemplateCatalog"/> -- this is the value a Director declares as a <c>Look</c>'s own <c>TemplateRef</c> when it draws on this template.</param>
/// <param name="Name">Short human-readable display name (e.g. <c>"EDM dense-canvas Tree/Matrix"</c>).</param>
/// <param name="EnergyBand">One of <c>"high"</c>, <c>"mid"</c>, <c>"low"</c> -- which section energy level this template suits.</param>
/// <param name="Props">Which prop families this template was derived from and targets (e.g. <c>"Tree"</c>, <c>"Matrixes"</c>, <c>"Mega Tree"</c>) -- a hint for selection, not a hard constraint any particular layout must satisfy.</param>
/// <param name="Motion">The <c>Look.Motion</c> value this template's overall feel maps to (<c>"hold"</c>, <c>"chase"</c>, <c>"build"</c>, <c>"pop"</c>, <c>"decay"</c>).</param>
/// <param name="Layers">This template's own layers, in declared z-order (index 0 is the base/bottom layer).</param>
/// <param name="SourceNote">Where this pattern was derived from and the licensing boundary it respects -- no literal palette or song-specific timing, only the generic structural shape.</param>
public sealed record MultiLayerTemplate(
    string Id,
    string Name,
    string EnergyBand,
    IReadOnlyList<string> Props,
    string Motion,
    IReadOnlyList<MultiLayerTemplateLayer> Layers,
    string SourceNote);

/// <summary>
/// The full shippable catalog -- <see cref="MultiLayerTemplateCatalogLoader"/>'s own return shape, and
/// what <c>DirectorKnowledgeContext</c> carries into the Director's stable prompt prefix. Versioned so a
/// later catalog revision (more templates, e.g. grown from the user's own future sequences per
/// build-plan Phase 10 §6b task 5) can be told apart from this first hand-curated one.
/// </summary>
public sealed record MultiLayerTemplateCatalog(int Version, DateTimeOffset GeneratedAtUtc, IReadOnlyList<MultiLayerTemplate> Templates)
{
    public MultiLayerTemplate? Find(string id) => Templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
}
