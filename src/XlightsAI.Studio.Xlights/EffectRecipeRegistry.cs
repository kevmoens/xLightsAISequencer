using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IEffectRecipeRegistry"/>. Step 012 ("three hardcoded simple effects") ships
/// exactly one known-good recipe, <see cref="SimpleOnRecipeId"/> — the simplest real xLights effect
/// (PHASE-02 Section 12 appendix's own illustrative <c>&lt;Effect name="On" ... /&gt;</c> shape).
/// <see cref="Parameters"/> on the produced <see cref="EffectInstance"/> names this recipe's own
/// input (which effect family it is), not a final xLights XML settings string — Section 3: "Store
/// recipe identity independent of final xLights XML settings representation." Translating a recipe
/// into actual xLights effect-settings XML is Step 013's own scope.
/// </summary>
public sealed class EffectRecipeRegistry : IEffectRecipeRegistry
{
    /// <summary>The one recipe this build currently recognizes: a constant-duration "On" effect.</summary>
    public const string SimpleOnRecipeId = "effect.on.v1";

    public const string SimpleOnRecipeVersion = "1";

    public EffectInstance CreateVerified(string recipeId, ModelId target, long startTicks, long endTicks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);

        if (string.IsNullOrWhiteSpace(target.Value))
        {
            throw new EffectPlanInvalidException("An effect's target model/group id must not be blank.");
        }

        if (startTicks < 0)
        {
            throw new EffectPlanInvalidException($"An effect's start time must not be negative (received {startTicks}).");
        }

        if (endTicks <= startTicks)
        {
            throw new EffectPlanInvalidException(
                $"An effect's end time ({endTicks}) must be strictly after its start time ({startTicks}) — the [start, end) window must be non-empty.");
        }

        if (recipeId != SimpleOnRecipeId)
        {
            throw new EffectPlanInvalidException($"Unknown effect recipe id '{recipeId}'. Known recipes: {SimpleOnRecipeId}.");
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EffectFamily"] = "On",
        };

        return new EffectInstance(Guid.NewGuid(), target, Layer: 0, startTicks, endTicks, recipeId, SimpleOnRecipeVersion, parameters);
    }

    /// <summary>
    /// PHASE-03 Step 020: <see cref="SimpleOnRecipeId"/>'s only known parameter is <c>EffectFamily</c>,
    /// and its only verified value is <c>"On"</c> (the only xLights effect this build currently emits,
    /// per this class's own <see cref="CreateVerified"/>). Any other key, any other value, or any other
    /// recipe id is unsupported — this registry never invents a second accepted value it has not
    /// verified against real xLights behavior.
    /// </summary>
    public bool IsSupportedParameterValue(string recipeId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        return recipeId == SimpleOnRecipeId
            && key == "EffectFamily"
            && value == "On";
    }
}
