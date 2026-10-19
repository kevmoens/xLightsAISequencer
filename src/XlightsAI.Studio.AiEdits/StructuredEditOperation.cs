using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.AiEdits;

/// <summary>
/// PHASE-11.md Section 3's own proposed signature, implemented verbatim: one bounded, typed, offline
/// edit action — never a free-form xLights XML fragment (Section 2: "Use a structured plan not
/// freeform XSQ"). <see cref="Kind"/> is always one of <see cref="StructuredEditOperationKinds"/>'
/// own closed vocabulary; <see cref="Range"/>/<see cref="Targets"/> are always a subset of the
/// originating <see cref="SelectionContext.Scope"/> this operation was parsed from — an operation can
/// never widen the time window or model set a user actually selected.
/// </summary>
/// <param name="OperationId">A caller/interpreter-assigned stable identity for this one operation, unique within its own <see cref="EditProposal.Operations"/> list.</param>
/// <param name="Kind">One of <see cref="StructuredEditOperationKinds"/>'s own named constants.</param>
/// <param name="Range">The exact half-open time window this operation is permitted to reach.</param>
/// <param name="Targets">The exact model set this operation is permitted to reach.</param>
/// <param name="Parameters">Kind-specific named values (e.g. <c>"Level"</c> for <see cref="StructuredEditOperationKinds.SetBrightness"/>), already range/vocabulary-validated by the interpreter that produced this operation.</param>
public sealed record StructuredEditOperation(
    string OperationId,
    string Kind,
    TimeWindow Range,
    IReadOnlyList<ModelId> Targets,
    IReadOnlyDictionary<string, string> Parameters);
