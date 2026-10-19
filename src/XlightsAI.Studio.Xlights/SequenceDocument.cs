namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Section 3 proposed contract: the editable sequence state. Step 012 only ever produces
/// an initial document (<see cref="Revision"/> <c>1</c>) carrying exactly the three hardcoded
/// effects this step is scoped to create; later phases (Section 1 cross-cutting table: "EditScope,
/// EditTransaction, DocumentRevision — Phase 03") own revision-aware mutation of an already-accepted
/// document. This step's own required acceptance — "Plan is serialized and inspectable independently
/// of XSQ" — is satisfied by this record being a plain, directly JSON-serializable value with no
/// dependency on <c>IXsqExporter</c>/XML types at all (<see cref="ISequenceDocumentStore"/>).
/// </summary>
public sealed record SequenceDocument(
    Guid Id,
    long Revision,
    IReadOnlyList<EffectInstance> Effects);
