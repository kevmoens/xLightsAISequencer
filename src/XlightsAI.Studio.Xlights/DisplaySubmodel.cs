namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md task 4): one <c>&lt;subModel&gt;</c> enumerated as its
/// own first-class addressable target (a megatree slice, a matrix region, a face part) rather than
/// only being recognized as a reference shape inside a group's member list
/// (<see cref="DisplayGroupMemberKind.Submodel"/>, unchanged). <see cref="NodeCount"/> is populated
/// only when the source XML's own <c>layout</c> attribute lets it be counted directly; otherwise it is
/// left <see langword="null"/> rather than guessed (same discipline as
/// <see cref="ShowLayoutImporter"/>'s existing preview-point parsing).
/// </summary>
public sealed record DisplaySubmodel(string Name, int? NodeCount);
