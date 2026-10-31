namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2a (docs/AI-CHOREOGRAPHY-BUILD-PLAN.md Phase 2 §2a; architecture §8a): one serializable
/// xLights effect property, generated from <c>effectmetadata/&lt;Effect&gt;.json</c> — never
/// hand-transcribed. <see cref="SettingKey"/> is the exact string xLights writes on the left of
/// <c>=</c> in an <c>&lt;Effect&gt;</c> settings string; <see cref="ValueCurveSettingKey"/> is the
/// alternate key xLights uses instead when a value curve is attached to this property (e.g.
/// <c>C_VALUECURVE_Brightness</c> replacing <c>C_SLIDER_Brightness</c>), present only when
/// <see cref="SupportsValueCurve"/> is true.
/// </summary>
public sealed record EffectPropertyVocabulary(
    string PropertyId,
    string SettingKey,
    string? ValueCurveSettingKey,
    string ControlType,
    string SemanticType,
    string? DefaultValue,
    double? Min,
    double? Max,
    int Divisor,
    bool SupportsValueCurve,
    IReadOnlyList<string>? Options,
    bool Lockable);

/// <summary>
/// One effect's (or one shared panel's) full set of generated, serializable properties.
/// <paramref name="Prefix"/> is <c>E_</c> for a real effect, or the shared panel's own declared prefix
/// (<c>B_</c>/<c>T_</c>/<c>C_</c>) — it is informational only, since every property already carries its
/// own fully-resolved <see cref="EffectPropertyVocabulary.SettingKey"/>.
/// </summary>
public sealed record EffectVocabulary(
    string EffectName,
    string Prefix,
    IReadOnlyList<EffectPropertyVocabulary> Properties)
{
    public EffectPropertyVocabulary? FindByPropertyId(string propertyId) =>
        Properties.FirstOrDefault(p => string.Equals(p.PropertyId, propertyId, StringComparison.Ordinal));
}
