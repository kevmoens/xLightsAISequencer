namespace XlightsAI.Studio.Display;

/// <summary>
/// PHASE-09 Step 065's own implementation guidance, verbatim: "Validate... effect support..." -- a
/// caller-declared policy of which recipe ids a given <see cref="DisplayNode.ModelKind"/> is known to
/// support. Deliberately supplied by the caller, never invented or hardcoded here (Section 2: "do not
/// invent xLights effect parameters") -- this project owns no recipe catalog of its own (recipes are
/// <see cref="Xlights.EffectInstance.RecipeId"/>, a Phase 02/03 concern); a later phase's own recipe
/// registry is expected to project its own per-model-kind support data into this shape when it calls
/// <see cref="IRouteValidator.Validate"/>.
/// </summary>
/// <param name="SupportedRecipeIdsByModelKind">
/// For each declared <see cref="DisplayNode.ModelKind"/>, the recipe ids known to be supported on it. A
/// model kind with no entry here is treated as unrestricted (no declared incompatibility is not evidence
/// of one) -- the same "absence is not a claim" convention <see cref="ResolutionSettings.SkipUnavailable"/>'s
/// own remarks already apply to missing nodes.
/// </param>
public sealed record EffectRecipeSupport(IReadOnlyDictionary<string, IReadOnlySet<string>> SupportedRecipeIdsByModelKind)
{
    /// <summary>No model kind declares any restriction -- every recipe is treated as compatible with every node. The safe default when no recipe registry has been wired in yet.</summary>
    public static EffectRecipeSupport Unrestricted { get; } = new(new Dictionary<string, IReadOnlySet<string>>());
}
