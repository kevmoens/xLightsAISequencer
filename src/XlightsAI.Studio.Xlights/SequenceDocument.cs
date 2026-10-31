namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: the editable sequence state. Step 012 only ever produces
/// an initial document (<see cref="Revision"/> <c>1</c>) carrying exactly the three hardcoded
/// effects this step is scoped to create; later phases (Section 1 cross-cutting table: "EditScope,
/// EditTransaction, DocumentRevision — Phase 03") own revision-aware mutation of an already-accepted
/// document. This step's own required acceptance — "Plan is serialized and inspectable independently
/// of XSQ" — is satisfied by this record being a plain, directly JSON-serializable value with no
/// dependency on <c>IXsqExporter</c>/XML types at all (<see cref="ISequenceDocumentStore"/>).
///
/// PHASE-1c (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md, "The intermediate representation"; architecture §12
/// blocker 3): <see cref="Metadata"/>, <see cref="Palettes"/> and <see cref="TimingTracks"/> are the
/// fields architecture §12 says this type needs to stop being "too thin to be the IR" — real sequence
/// metadata, a palette table, and timing tracks as first-class content (architecture §6). Each is a
/// trailing optional parameter, same convention as <see cref="EffectInstance.Locked"/>, so every
/// existing Step 012-093 construction site across the whole solution keeps compiling unchanged when it
/// does not care about any of them; <see cref="XsqExporter"/> falls back to exactly its previous
/// single-default-palette, no-timing-track, empty-metadata output when all three are omitted (the
/// golden fixture is unaffected by this type's introduction).
/// </summary>
public sealed record SequenceDocument(
    Guid Id,
    long Revision,
    IReadOnlyList<EffectInstance> Effects,
    SequenceMetadata? Metadata = null,
    IReadOnlyList<ColorPaletteDefinition>? Palettes = null,
    IReadOnlyList<TimingTrack>? TimingTracks = null)
{
    public SequenceMetadata MetadataOrDefault => Metadata ?? new SequenceMetadata();

    public IReadOnlyList<ColorPaletteDefinition> PalettesOrDefault =>
        Palettes is { Count: > 0 } ? Palettes : [new ColorPaletteDefinition("C_BUTTON_Palette1=#FFFFFF")];

    public IReadOnlyList<TimingTrack> TimingTracksOrEmpty => TimingTracks ?? [];
}
