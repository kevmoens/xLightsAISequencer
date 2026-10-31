using XlightsAI.Studio.Knowledge;

namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-2a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md Phase 2 §2a.5; architecture §8a/§12 blocker 2): one
/// <see cref="IEffectRecipe"/> built directly from a generated <see cref="EffectVocabulary"/> instead
/// of a hand-written class per effect. Registering every effect xLights ships means constructing one
/// of these per <see cref="EffectVocabulary"/> and calling <see cref="EffectRecipeCatalog.Register"/> —
/// see <see cref="GeneratedEffectRecipeFactory.RegisterAll"/> — never editing
/// <see cref="EffectRecipeRegistry"/>, <see cref="XsqExporter"/> or a plan factory, honoring the same
/// "no parallel edits" extension point <see cref="OnEffectRecipe"/> already established.
/// <see cref="IsSupportedParameterValue"/> defers entirely to
/// <see cref="EffectSettingsValidator"/>, so acceptance is exactly what the generated metadata
/// states — never invented.
/// </summary>
public sealed class GeneratedEffectRecipe : IEffectRecipe
{
    private readonly EffectVocabulary _vocabulary;
    private readonly EffectSettingsValidator _validator;

    public GeneratedEffectRecipe(EffectVocabulary vocabulary, EffectVocabularyCatalog catalog, string recipeVersion = "1")
    {
        _vocabulary = vocabulary ?? throw new ArgumentNullException(nameof(vocabulary));
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeVersion);

        _validator = new EffectSettingsValidator(catalog);
        RecipeVersion = recipeVersion;
    }

    /// <summary>Deterministic recipe id derived from the real xLights effect name, e.g. <c>effect.generated.bars.v1</c>.</summary>
    public string RecipeId => $"effect.generated.{_vocabulary.EffectName.ToLowerInvariant()}.v{RecipeVersion}";

    public string RecipeVersion { get; }

    public string XlightsEffectName => _vocabulary.EffectName;

    public bool IsSupportedParameterValue(string key, string value) =>
        _validator.IsSupportedParameterValue(_vocabulary.EffectName, key, value);

    /// <summary>
    /// Every one of this effect's own properties whose generated metadata states a default, keyed by
    /// its fully-resolved setting key — the "fully-verified typed parameter set" <see cref="IEffectRecipe"/>
    /// requires, sourced from the metadata rather than guessed. Properties whose default is unknown
    /// (the handful of hardcoded legacy exceptions architecture §8a documents, e.g.
    /// <c>EffectLayerMix</c>) are omitted rather than assigned an invented value.
    /// </summary>
    public IReadOnlyDictionary<string, string> CreateDefaultParameters() =>
        _vocabulary.Properties
            .Where(p => p.DefaultValue is not null)
            .ToDictionary(p => p.SettingKey, p => p.DefaultValue!, StringComparer.Ordinal);
}
