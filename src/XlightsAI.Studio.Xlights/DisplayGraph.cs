namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: the full, read-only, normalized snapshot of one show
/// directory's <c>xlights_rgbeffects.xml</c> models/groups/geometry. <see cref="SourceLayoutHash"/>
/// is the SHA-256 of the exact bytes parsed (Section 4: "Store read-only show path, source layout
/// hash, imported model/group snapshot... in project DB") — it identifies *which* on-disk XML this
/// snapshot came from without re-reading the file, and changes the instant the source file's bytes
/// change.
///
/// PHASE-02 Step 011 additive extension: <see cref="Groups"/> exposes every <c>&lt;modelGroup&gt;</c>
/// as its own targetable entity with each raw member classified (model/group/submodel/unresolved);
/// <see cref="Warnings"/> carries every non-destructive finding from that validation (broken
/// references, a missing <c>&lt;models&gt;</c>/<c>&lt;modelGroups&gt;</c> element) — the import itself
/// still always succeeds and returns a complete graph (PHASE-02 Step 011 required acceptance:
/// "Fixture with broken references generates warnings rather than partial destructive output").
/// </summary>
public sealed record DisplayGraph(
    string SourceLayoutHash,
    IReadOnlyList<DisplayModel> Models,
    IReadOnlyList<DisplayModelGroup> Groups,
    IReadOnlyList<string> Warnings);
