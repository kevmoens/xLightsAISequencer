using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Section 3 proposed contract: one relationship between two <see cref="DisplayNode"/>s.
/// Step 059 only ever produces <see cref="Relation"/> values <c>"GroupMember"</c> (a model directly
/// listed in a group's own member list -- <see cref="From"/> is the model, <see cref="To"/> is the
/// group) and <c>"NestedGroup"</c> (a group listed as another group's own member -- <see cref="From"/>
/// is the nested group, <see cref="To"/> is the parent). Neighbor/left-right/above-below relations
/// (Section 3's own broader "Edge relationships include neighbor, higher/lower...") are Step 060's own
/// later scope -- deliberately not produced here.
/// </summary>
/// <param name="From">The member-side node.</param>
/// <param name="To">The group-side node.</param>
/// <param name="Relation">A stable relation-kind discriminator; not an enum so later steps (060+) can add new kinds without a breaking contract change here.</param>
/// <param name="Distance">Euclidean distance between <see cref="From"/> and <see cref="To"/>'s normalized <see cref="DisplayNode.X"/>/<see cref="DisplayNode.Y"/>; <c>0</c> when either node's geometry could not be resolved (<c>"NoGeometry"</c> tag present) -- never a guessed, non-zero value.</param>
/// <param name="UserDefined">Always <c>false</c> for every edge Step 059 itself produces -- group membership is derived from the imported layout, not authored by a user action. Reserved for Steps 061+'s own user-defined edges.</param>
/// <param name="Confidence">
/// PHASE-09 Step 060 additive field (default <c>1.0</c>; see
/// <c>implementation-evidence/PHASE-09/STEP-060.md</c> §3 for the disclosed rationale): how confident the
/// producer is that this edge reflects true physical/artistic reality, in <c>[0, 1]</c>. Step 059's own
/// <c>"GroupMember"</c>/<c>"NestedGroup"</c> edges are a literal fact read from the imported layout, not an
/// inference, so they always keep the default <c>1.0</c>. Step 060's own inferred
/// <c>"Neighbor"</c>/<c>"LeftOf"</c>/<c>"Below"</c> edges set this below <c>1.0</c> when the underlying
/// geometry is an outlier relative to its own cluster's typical spacing (Section 3's own instruction:
/// "surface confidence rather than assert physical truth"). The default keeps every pre-Step-060 persisted
/// <see cref="SpatialDisplayGraph"/> cache file (written before this field existed) loadable without
/// corruption or a forced migration.
/// </param>
public sealed record SpatialEdge(ModelId From, ModelId To, string Relation,
    double Distance, bool UserDefined, double Confidence = 1.0);
