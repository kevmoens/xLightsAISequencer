using System.Text.Json.Serialization;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3 proposed contract: one node of the <see cref="SpatialDisplayGraph"/> -- either a
/// real imported <c>DisplayModel</c> (<see cref="ModelKind"/> carries that model's own xLights
/// <c>DisplayAs</c> value) or a <c>DisplayModelGroup</c> re-projected as its own targetable node
/// (<see cref="ModelKind"/> is the literal <c>"Group"</c> marker -- Step 059's own "model groups...
/// into display graph" scope; PHASE-02's own prior note that "a group is itself a valid effect target
/// in xLights, distinct from its members" applies equally here).
/// </summary>
/// <param name="Id">
/// This node's stable identity. For a model, this is derived from the model's current xLights name
/// unless an explicit <see cref="NodeAlias"/> pins it to a prior, pre-rename identity (Step 059's own
/// "controlled alias resolution for changing xLights model names" -- never automatic/fuzzy). For a
/// group, this is derived from the group's own name. Deliberately the same <see cref="ModelId"/> type
/// Phase 02's <c>DisplayModel.Id</c> uses, but a <em>separate</em> identity space -- a renamed model's
/// node id can diverge from its current raw xLights name (Section 2's cross-phase ownership table:
/// "normalized geometry separate from imported xLights identities").
/// </param>
/// <param name="XlightsName">The model/group's current, as-imported xLights name, preserved verbatim for display/round-trip -- never itself treated as a stable identity (<see cref="Id"/> is).</param>
/// <param name="X">Normalized preview X in <c>[0, 1]</c> (Section 3: "Store normalized 2D coordinates only for initial preview"). <c>0.5</c> when no source geometry could be resolved (never guessed from neighbors).</param>
/// <param name="Y">Normalized preview Y in <c>[0, 1]</c>, independently min-max scaled from <see cref="X"/> (no aspect-ratio assumption is made about the source layout).</param>
/// <param name="ModelKind">The model's own xLights <c>DisplayAs</c> value, or the literal <c>"Group"</c> for a group node.</param>
/// <param name="Tags">Free-form provenance markers -- e.g. <c>"NoGeometry"</c> when <see cref="X"/>/<see cref="Y"/> could not be resolved from any source point. Never used for business-rule branching outside this project.</param>
public sealed record DisplayNode(ModelId Id, string XlightsName, double X, double Y,
    string ModelKind, [property: JsonConverter(typeof(ReadOnlyStringSetJsonConverter))] IReadOnlySet<string> Tags);
