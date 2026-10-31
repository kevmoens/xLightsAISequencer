using System.Globalization;

namespace XlightsAI.Studio.Knowledge;

/// <summary>Why <see cref="EffectSettingsValidator"/> rejected one key/value pair.</summary>
public enum EffectSettingValidationErrorKind
{
    UnknownProperty,
    OutOfRange,
    InvalidChoice,
    InvalidValueCurve,
    MalformedValue,
}

public sealed record EffectSettingValidationError(string Key, EffectSettingValidationErrorKind Kind, string Message);

/// <summary>
/// PHASE-2a: validates a proposed effect setting against the real, generated vocabulary — never
/// invents acceptance for anything the metadata doesn't actually state, mirroring the discipline
/// <see cref="XlightsAI.Studio.Knowledge"/>'s sibling project documents on
/// <c>XlightsAI.Studio.Xlights.IEffectRecipe.IsSupportedParameterValue</c>: an unrecognized key, an
/// out-of-range number, an invalid enum choice, or a value curve on a property that doesn't accept one
/// is always rejected, never guessed at.
/// </summary>
public sealed class EffectSettingsValidator
{
    private readonly EffectVocabularyCatalog _catalog;

    public EffectSettingsValidator(EffectVocabularyCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// True only when <paramref name="value"/> is a verified-acceptable plain (non-value-curve) value
    /// for the fully-qualified setting <paramref name="settingKey"/> on <paramref name="effectName"/>.
    /// </summary>
    public bool IsSupportedParameterValue(string effectName, string settingKey, string value) =>
        TryValidatePlainValue(effectName, settingKey, value) is null;

    /// <summary>
    /// True only when <paramref name="settingKey"/> is the value-curve key of a property that actually
    /// declares <c>"valueCurve": true</c> — the structural rejection this project uses for "a value
    /// curve attached to a property whose valueCurve is false": such a key was never registered as a
    /// value-curve key in <see cref="EffectVocabularyCatalog"/> to begin with.
    /// </summary>
    public bool IsSupportedValueCurve(string effectName, string settingKey)
    {
        if (!_catalog.TryGetProperty(effectName, settingKey, out var property))
        {
            return false;
        }

        return property.SupportsValueCurve && string.Equals(property.ValueCurveSettingKey, settingKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates a full proposed plain-settings dictionary (as the Choreography/Compilation layers would
    /// build one before serializing), returning every violation found — empty means every key/value is
    /// acceptable. Does not validate value curves; use <see cref="IsSupportedValueCurve"/> for those.
    /// </summary>
    public IReadOnlyList<EffectSettingValidationError> ValidateSettings(string effectName, IReadOnlyDictionary<string, string> settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectName);
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<EffectSettingValidationError>();
        foreach (var (key, value) in settings)
        {
            var error = TryValidatePlainValue(effectName, key, value);
            if (error is not null)
            {
                errors.Add(error);
            }
        }

        return errors;
    }

    private EffectSettingValidationError? TryValidatePlainValue(string effectName, string settingKey, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(settingKey);
        ArgumentNullException.ThrowIfNull(value);

        if (!_catalog.TryGetProperty(effectName, settingKey, out var property))
        {
            return new EffectSettingValidationError(settingKey, EffectSettingValidationErrorKind.UnknownProperty,
                $"'{settingKey}' is not a known setting for effect '{effectName}'.");
        }

        if (string.Equals(property.ValueCurveSettingKey, settingKey, StringComparison.Ordinal))
        {
            return new EffectSettingValidationError(settingKey, EffectSettingValidationErrorKind.InvalidValueCurve,
                $"'{settingKey}' is a value-curve key; use {nameof(IsSupportedValueCurve)} to validate a curve, not a plain value.");
        }

        if (property.Options is { Count: > 0 })
        {
            return property.Options.Contains(value, StringComparer.Ordinal)
                ? null
                : new EffectSettingValidationError(settingKey, EffectSettingValidationErrorKind.InvalidChoice,
                    $"'{value}' is not one of the valid choices for '{settingKey}' ({string.Join(", ", property.Options)}).");
        }

        if (property.SemanticType is "int" or "float")
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                return new EffectSettingValidationError(settingKey, EffectSettingValidationErrorKind.MalformedValue,
                    $"'{value}' is not a valid number for '{settingKey}'.");
            }

            if (property.Min is { } min && numeric < min)
            {
                return new EffectSettingValidationError(settingKey, EffectSettingValidationErrorKind.OutOfRange,
                    $"{numeric} is below the minimum ({min}) for '{settingKey}'.");
            }

            if (property.Max is { } max && numeric > max)
            {
                return new EffectSettingValidationError(settingKey, EffectSettingValidationErrorKind.OutOfRange,
                    $"{numeric} is above the maximum ({max}) for '{settingKey}'.");
            }
        }

        return null;
    }
}
