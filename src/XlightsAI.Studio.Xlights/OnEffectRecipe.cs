namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1b: the one recipe this build has shipped since Step 012, now expressed as an
/// <see cref="IEffectRecipe"/> instead of being hardcoded inline inside
/// <see cref="EffectRecipeRegistry"/> — the simplest real xLights effect (a constant-duration "On"),
/// with an empty, all-default settings string (confirmed correct and idiomatic against real xLights
/// output; see docs/PHASE0-FINDINGS.md).
/// </summary>
public sealed class OnEffectRecipe : IEffectRecipe
{
    public const string Id = "effect.on.v1";
    public const string Version = "1";

    public string RecipeId => Id;

    public string RecipeVersion => Version;

    public string XlightsEffectName => "On";

    public bool IsSupportedParameterValue(string key, string value) =>
        key == "EffectFamily" && value == "On";

    public IReadOnlyDictionary<string, string> CreateDefaultParameters() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EffectFamily"] = "On",
        };
}
