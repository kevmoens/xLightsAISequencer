using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: one normalized, stably-identified model imported (read-only)
/// from <c>xlights_rgbeffects.xml</c>'s <c>&lt;models&gt;</c>/<c>&lt;modelGroups&gt;</c> structures.
/// <see cref="Id"/> is derived from the model's own xLights <c>name</c> attribute (trimmed, never
/// re-cased) — the only stable identity xLights itself assigns a model by; <see cref="XlightsName"/>
/// preserves that same original name verbatim for display/round-trip purposes (Section 3
/// "Architectural design notes": "retain original qualified names"). <see cref="GroupNames"/> lists
/// every <c>&lt;modelGroup&gt;</c> this model's name appears in — a model may belong to more than one
/// group; group membership is never flattened/deduplicated away (Step 011's own later concern).
/// <see cref="PreviewPoints"/> is populated only when the source XML actually carries a world
/// position for this model ("geometry where present"); otherwise it is empty, never guessed.
/// </summary>
public sealed record DisplayModel(
    ModelId Id,
    string XlightsName,
    string Kind,
    IReadOnlyList<string> GroupNames,
    IReadOnlyList<Point2> PreviewPoints);
