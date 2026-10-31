namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-1b (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md task 7): the registration mechanism architecture §12
/// blocker 2 calls for — one place a new <see cref="IEffectRecipe"/> is added
/// (<see cref="Register"/>), resolvable by id, with no parallel edits required in
/// <see cref="EffectRecipeRegistry"/>, <see cref="XsqExporter"/> or a plan factory. The actual recipe
/// *catalog* generated from xLights' effect metadata is Phase 2's scope (architecture §8a); this is
/// only the shape that catalog will register into, pre-populated with the one recipe this build
/// already ships.
/// </summary>
public sealed class EffectRecipeCatalog
{
    private readonly Dictionary<string, IEffectRecipe> _recipesById = new(StringComparer.Ordinal);

    /// <summary>A catalog pre-populated with every recipe this build currently ships.</summary>
    public static EffectRecipeCatalog CreateDefault()
    {
        var catalog = new EffectRecipeCatalog();
        catalog.Register(new OnEffectRecipe());
        return catalog;
    }

    /// <summary>Registers <paramref name="recipe"/>, replacing any existing recipe already registered under the same <see cref="IEffectRecipe.RecipeId"/>.</summary>
    public void Register(IEffectRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        _recipesById[recipe.RecipeId] = recipe;
    }

    public bool TryGet(string recipeId, out IEffectRecipe recipe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        return _recipesById.TryGetValue(recipeId, out recipe!);
    }

    /// <summary>Every recipe currently registered, for diagnostics (e.g. "unknown recipe id, known recipes: ...").</summary>
    public IReadOnlyCollection<IEffectRecipe> Recipes => _recipesById.Values;
}
