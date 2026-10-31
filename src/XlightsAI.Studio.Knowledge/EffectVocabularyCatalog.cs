namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// The whole generated effect vocabulary: every real xLights effect plus the three shared panels
/// (Buffer/Blending/Color) that apply to every effect regardless of its own name. Built once by
/// <see cref="EffectVocabularyBuilder"/> and then used read-only by
/// <see cref="EffectSettingsValidator"/>, <see cref="EffectSettingsSerializer"/> and
/// <see cref="EffectVocabularySchemaGenerator"/>.
/// </summary>
public sealed class EffectVocabularyCatalog
{
    private readonly Dictionary<string, Dictionary<string, EffectPropertyVocabulary>> _effectKeyIndex;
    private readonly Dictionary<string, EffectPropertyVocabulary> _sharedKeyIndex;

    public EffectVocabularyCatalog(
        IReadOnlyDictionary<string, EffectVocabulary> effects,
        EffectVocabulary buffer,
        EffectVocabulary blending,
        EffectVocabulary color)
    {
        Effects = effects;
        Buffer = buffer;
        Blending = blending;
        Color = color;

        _effectKeyIndex = effects.ToDictionary(
            kv => kv.Key,
            kv => BuildKeyIndex(kv.Value.Properties),
            StringComparer.Ordinal);

        _sharedKeyIndex = BuildKeyIndex(buffer.Properties.Concat(blending.Properties).Concat(color.Properties));
    }

    /// <summary>Every real xLights effect's vocabulary, keyed by its <c>effectName</c> (e.g. <c>"Bars"</c>, <c>"On"</c>).</summary>
    public IReadOnlyDictionary<string, EffectVocabulary> Effects { get; }

    /// <summary>The <c>B_</c> buffer panel, present on every effect.</summary>
    public EffectVocabulary Buffer { get; }

    /// <summary>The <c>T_</c> blending/timing panel, present on every effect.</summary>
    public EffectVocabulary Blending { get; }

    /// <summary>The <c>C_</c> color panel (including the 16 hardcoded palette slots), present on every effect.</summary>
    public EffectVocabulary Color { get; }

    /// <summary>
    /// Resolves a fully-qualified setting key (e.g. <c>E_SLIDER_Bars_BarCount</c>, <c>T_CHOICE_LayerMethod</c>,
    /// <c>C_BUTTON_Palette1</c>, or the bare <c>SLIDER_Rotations</c>) against <paramref name="effectName"/>'s
    /// own properties first, then the shared Buffer/Blending/Color panels every effect carries. Returns
    /// <see langword="false"/> — never a guessed/invented match — for anything neither recognizes.
    /// </summary>
    public bool TryGetProperty(string effectName, string settingKey, out EffectPropertyVocabulary property)
    {
        if (_effectKeyIndex.TryGetValue(effectName, out var effectIndex) &&
            effectIndex.TryGetValue(settingKey, out property!))
        {
            return true;
        }

        return _sharedKeyIndex.TryGetValue(settingKey, out property!);
    }

    private static Dictionary<string, EffectPropertyVocabulary> BuildKeyIndex(IEnumerable<EffectPropertyVocabulary> properties)
    {
        var index = new Dictionary<string, EffectPropertyVocabulary>(StringComparer.Ordinal);
        foreach (var property in properties)
        {
            index[property.SettingKey] = property;
            if (property.ValueCurveSettingKey is { } vcKey)
            {
                index[vcKey] = property;
            }
        }

        return index;
    }
}
