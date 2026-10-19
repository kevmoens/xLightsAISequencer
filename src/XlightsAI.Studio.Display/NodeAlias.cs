using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 059's own "controlled alias resolution for changing xLights model names": one
/// explicit, caller-supplied statement that the model currently named <see cref="XlightsName"/> in a
/// freshly re-imported layout is the <em>same physical prop</em> as the previously stable node
/// <see cref="NodeId"/> -- never inferred automatically (no fuzzy/distance-based name matching; a
/// renamed model that has no matching alias simply becomes a new node, with the old node id no longer
/// present, surfaced for the caller to notice and reconcile). Resolved by name only (not by prior
/// geometry) because xLights carries no other persistent identity for a model across a rename.
/// </summary>
public sealed record NodeAlias(ModelId NodeId, string XlightsName);
