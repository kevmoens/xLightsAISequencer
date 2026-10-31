using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// Default <see cref="IEffectRecipeRegistry"/>. PHASE-1b: resolves recipe ids against an
/// <see cref="EffectRecipeCatalog"/> (defaulting to <see cref="EffectRecipeCatalog.CreateDefault"/>)
/// instead of a single hardcoded <c>if</c> — adding a recipe now means registering an
/// <see cref="IEffectRecipe"/> in the catalog, not editing this class. <see cref="SimpleOnRecipeId"/>/
/// <see cref="SimpleOnRecipeVersion"/> are kept as the same public constants every existing Step
/// 012-093 call site already references, now forwarding to <see cref="OnEffectRecipe"/>.
/// </summary>
public sealed class EffectRecipeRegistry : IEffectRecipeRegistry
{
    /// <summary>The one recipe this build ships by default: a constant-duration "On" effect.</summary>
    public const string SimpleOnRecipeId = OnEffectRecipe.Id;

    public const string SimpleOnRecipeVersion = OnEffectRecipe.Version;

    private readonly EffectRecipeCatalog _catalog;

    public EffectRecipeRegistry()
        : this(EffectRecipeCatalog.CreateDefault())
    {
    }

    /// <summary>Accepts a caller-supplied catalog (e.g. one with additional recipes registered) instead of only the default one.</summary>
    public EffectRecipeRegistry(EffectRecipeCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

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

        if (!_catalog.TryGet(recipeId, out var recipe))
        {
            var known = string.Join(", ", _catalog.Recipes.Select(r => r.RecipeId));
            throw new EffectPlanInvalidException($"Unknown effect recipe id '{recipeId}'. Known recipes: {known}.");
        }

        return new EffectInstance(
            Guid.NewGuid(), target, Layer: 0, startTicks, endTicks, recipe.RecipeId, recipe.RecipeVersion, recipe.CreateDefaultParameters());
    }

    /// <summary>
    /// PHASE-03 Step 020: delegates to the resolved recipe's own
    /// <see cref="IEffectRecipe.IsSupportedParameterValue"/> — never invents a supported value for an
    /// unregistered recipe id, returning <see langword="false"/> instead.
    /// </summary>
    public bool IsSupportedParameterValue(string recipeId, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        return _catalog.TryGet(recipeId, out var recipe) && recipe.IsSupportedParameterValue(key, value);
    }
}
