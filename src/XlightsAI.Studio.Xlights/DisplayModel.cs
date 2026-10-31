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
///
/// PHASE-1a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md, "Layout geometry"): <see cref="Category"/>,
/// <see cref="StringType"/>, <see cref="Parm1"/>/<see cref="Parm2"/>/<see cref="Parm3"/>,
/// <see cref="WindowRows"/>/<see cref="WindowColumns"/>, <see cref="NodeCount"/>, <see cref="Bounds"/>
/// and <see cref="Submodels"/> are the real pixel geometry architecture §12 blocker 1 calls out as
/// missing ("Pixel-weighted coverage — the single most important correctness mechanism in this design
/// — is impossible until the importer reads node counts and real geometry"). Every one of these is
/// appended as a trailing optional parameter (the same convention <see cref="EffectInstance.Locked"/>
/// already established) so every existing Step 009-093 construction site across the whole solution
/// keeps compiling and behaving identically when it does not care about geometry.
/// </summary>
public sealed record DisplayModel(
    ModelId Id,
    string XlightsName,
    string Kind,
    IReadOnlyList<string> GroupNames,
    IReadOnlyList<Point2> PreviewPoints,
    ModelCategory Category = ModelCategory.Other,
    string StringType = "",
    int Parm1 = 0,
    int Parm2 = 0,
    int Parm3 = 0,
    int? WindowRows = null,
    int? WindowColumns = null,
    int NodeCount = 0,
    DisplayBounds? Bounds = null,
    IReadOnlyList<DisplaySubmodel>? Submodels = null)
{
    /// <summary>Never <see langword="null"/>, regardless of whether a caller supplied <see cref="Submodels"/>.</summary>
    public IReadOnlyList<DisplaySubmodel> SubmodelsOrEmpty => Submodels ?? [];
}
