namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 011 ("Identify targetable models versus groups/submodels, reject unresolved refs"):
/// how one raw, as-written member entry of a <c>&lt;modelGroup&gt;</c>'s <c>models</c> attribute
/// resolves against this same document's own parsed models and groups.
/// </summary>
public enum DisplayGroupMemberKind
{
    /// <summary>The raw name matches a real, directly targetable <see cref="DisplayModel"/> by name.</summary>
    Model,

    /// <summary>The raw name matches another <c>&lt;modelGroup&gt;</c>'s own name — a nested group reference, never flattened/expanded here.</summary>
    Group,

    /// <summary>
    /// The raw name has the xLights <c>ModelName/SubModelName</c> shape and its model part matches a
    /// real model; the submodel part itself is not independently verified (this document format does
    /// not enumerate a model's own submodels, so only the model half of the reference can be checked).
    /// </summary>
    Submodel,

    /// <summary>The raw name matches neither a known model, a known group, nor a known model's submodel shape — a broken reference.</summary>
    Unresolved,
}
