using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 020's own command: the "edit supported effect parameters" half of this step's
/// title. <see cref="ParameterChanges"/> carries only the keys the caller wants to change; every key
/// already present on the target <see cref="XlightsAI.Studio.Xlights.EffectInstance.Parameters"/> that
/// is not named here is left exactly as-is (Step 020 implementation guidance: "preserve unchanged
/// effect fields"). Every key/value pair is validated against the effect's own
/// <see cref="XlightsAI.Studio.Xlights.IEffectRecipeRegistry"/> recipe before being applied — an
/// unrecognized key or an unsupported value is rejected, never silently invented (Section 2: "do not
/// invent xLights effect parameters").
/// </summary>
public sealed record SetEffectParameters(
    DocumentRevision ExpectedRevision,
    EditScope Scope,
    Guid EffectId,
    IReadOnlyDictionary<string, string> ParameterChanges) : EditCommand(ExpectedRevision, Scope);
