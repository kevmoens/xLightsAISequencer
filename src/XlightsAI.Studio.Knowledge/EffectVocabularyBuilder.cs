using System.Globalization;
using System.Text.Json;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2a: parses xLights' <c>resources/effectmetadata/&lt;Effect&gt;.json</c> tree — all 56 real
/// effects plus the three shared panels — into the processed <see cref="EffectVocabularyCatalog"/> this
/// project validates, serializes and schema-generates against. A build-time/dev-time tool: the caller
/// supplies the metadata root directory (e.g. <c>C:\Program Files\xLights\effectmetadata</c> on the
/// developer's machine), never a hardcoded path baked into the shipped product.
/// </summary>
public static class EffectVocabularyBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Builds the full catalog from <paramref name="metadataRootDirectory"/>, which must contain one
    /// <c>.json</c> file per real effect (anything except <c>_schema.json</c>) plus a <c>shared</c>
    /// subdirectory with <c>Buffer.json</c>, <c>Blending.json</c> and <c>Color.json</c>.
    /// </summary>
    public static EffectVocabularyCatalog BuildFromDirectory(string metadataRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataRootDirectory);
        if (!Directory.Exists(metadataRootDirectory))
        {
            throw new DirectoryNotFoundException($"Effect metadata directory not found: '{metadataRootDirectory}'.");
        }

        var sharedDirectory = Path.Combine(metadataRootDirectory, "shared");

        var buffer = BuildSharedPanel(Path.Combine(sharedDirectory, "Buffer.json"), "B_");
        var blending = BuildSharedPanel(Path.Combine(sharedDirectory, "Blending.json"), "T_");
        var color = BuildSharedPanel(Path.Combine(sharedDirectory, "Color.json"), "C_");

        // Architecture §8a known hardcoded exceptions: LayerMethod/LayerMorph/EffectLayerMix are not
        // present as `properties` entries anywhere in the shipped JSON (Blending.json only documents
        // them in prose on the compound custom rows that host them), yet they are real, commonly-set
        // settings. Modeled explicitly here rather than invented a plausible-looking JSON entry for.
        blending = blending with { Properties = [.. blending.Properties, .. BuildLegacyBlendingProperties()] };
        buffer = buffer with { Properties = [.. buffer.Properties, BuildLayerMethodOnBuffer()] };

        // Architecture §8a / §5: palette colors are serialized manually by ColorPanel::GetColorString
        // and are deliberately absent from Color.json's own `properties` array (see its `notes`) — model
        // the fixed 8 button + 8 checkbox slots explicitly instead of expecting to mine them from JSON.
        color = color with { Properties = [.. color.Properties, .. BuildPaletteProperties()] };

        var effects = new Dictionary<string, EffectVocabulary>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(metadataRootDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFileName(file), "_schema.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var vocabulary = BuildEffectVocabulary(file, "E_");
            effects[vocabulary.EffectName] = vocabulary;
        }

        return new EffectVocabularyCatalog(effects, buffer, blending, color);
    }

    private static EffectVocabulary BuildEffectVocabulary(string jsonFilePath, string defaultPrefix)
    {
        var raw = ParseFile(jsonFilePath);
        var prefix = raw.Prefix ?? defaultPrefix;
        var properties = raw.Properties
            .Where(p => !string.Equals(p.ControlType, "custom", StringComparison.OrdinalIgnoreCase) || IsFacesMouthMovementsOverride(raw.EffectName, p.Id))
            .Select(p => IsFacesMouthMovementsOverride(raw.EffectName, p.Id) ? AsChoiceProperty(p) : p)
            .SelectMany(p => ConvertProperty(p, prefix))
            .ToList();

        return new EffectVocabulary(raw.EffectName, prefix, properties);
    }

    /// <summary>
    /// Build-plan Phase 8 task 3 (singing-face support). The real shipped <c>Faces.json</c> declares
    /// <c>Faces_MouthMovements</c> (the property that picks a fixed phoneme OR a lyric/phoneme timing
    /// track) as <c>controlType: "custom"</c> -- xLights' own custom combo-plus-timing-track-picker
    /// control, not a plain choice list. Every other <c>"custom"</c> property across the shipped metadata
    /// is correctly dropped (no generic representation exists for it), but dropping this one specific
    /// property would make <c>Faces</c> impossible to bind to a timing track at all -- the one real
    /// xLights effect the "lyric track -&gt; Faces" pipeline depends on. Modeled here as an ordinary
    /// choice property (options = the real xLights viseme/phoneme code set) so it survives the generic
    /// filter and gets a real <c>SettingKey</c>; same documented-hardcoded-exception discipline as
    /// <see cref="BuildLegacyBlendingProperties"/>.
    /// </summary>
    private static bool IsFacesMouthMovementsOverride(string effectName, string propertyId) =>
        string.Equals(effectName, "Faces", StringComparison.Ordinal) && string.Equals(propertyId, "Faces_MouthMovements", StringComparison.Ordinal);

    private static readonly List<string> FacesMouthMovementOptions = ["AI", "E", "FV", "L", "MBP", "O", "U", "WQ", "rest"];

    private static EffectMetadataPropertyJson AsChoiceProperty(EffectMetadataPropertyJson p) => new()
    {
        Id = p.Id,
        Label = p.Label,
        Type = p.Type,
        ControlType = "choice",
        Default = p.Default is null or "" ? "rest" : p.Default,
        Options = FacesMouthMovementOptions,
        Description = p.Description,
    };

    private static EffectVocabulary BuildSharedPanel(string jsonFilePath, string expectedPrefix) =>
        BuildEffectVocabulary(jsonFilePath, expectedPrefix);

    private static EffectMetadataFileJson ParseFile(string jsonFilePath)
    {
        using var stream = File.OpenRead(jsonFilePath);
        var parsed = JsonSerializer.Deserialize<EffectMetadataFileJson>(stream, JsonOptions);
        return parsed ?? throw new InvalidDataException($"'{jsonFilePath}' did not deserialize to effect metadata.");
    }

    /// <summary>
    /// Converts one raw JSON property into zero (custom, already filtered out before this is called),
    /// one, or two (point2d expands into an X and a Y row) <see cref="EffectPropertyVocabulary"/> entries.
    /// </summary>
    private static IEnumerable<EffectPropertyVocabulary> ConvertProperty(EffectMetadataPropertyJson p, string panelPrefix)
    {
        if (string.Equals(p.ControlType, "point2d", StringComparison.OrdinalIgnoreCase))
        {
            yield return ConvertScalarProperty(
                p, panelPrefix, idSuffix: "X",
                min: p.MinX ?? p.Min, max: p.MaxX ?? p.Max, defaultValue: p.DefaultX ?? p.Default);
            yield return ConvertScalarProperty(
                p, panelPrefix, idSuffix: "Y",
                min: p.MinY ?? p.Min, max: p.MaxY ?? p.Max, defaultValue: p.DefaultY ?? p.Default);
            yield break;
        }

        yield return ConvertScalarProperty(p, panelPrefix, idSuffix: null, min: p.Min, max: p.Max, defaultValue: p.Default);
    }

    private static EffectPropertyVocabulary ConvertScalarProperty(
        EffectMetadataPropertyJson p, string panelPrefix, string? idSuffix, double? min, double? max, object? defaultValue)
    {
        var propertyId = idSuffix is null ? p.Id : $"{p.Id}{idSuffix}";
        var divisor = p.Divisor is null or 0 ? 1 : p.Divisor.Value;
        var settingPrefixToken = ResolveSettingPrefixToken(p, divisor);

        var settingKey = BuildSettingKey(panelPrefix, settingPrefixToken, propertyId, p.Id);
        var valueCurveSettingKey = p.ValueCurve
            ? BuildValueCurveSettingKey(panelPrefix, propertyId, p.Id)
            : null;

        return new EffectPropertyVocabulary(
            PropertyId: propertyId,
            SettingKey: settingKey,
            ValueCurveSettingKey: valueCurveSettingKey,
            ControlType: p.ControlType,
            SemanticType: p.Type,
            DefaultValue: FormatDefaultValue(defaultValue, divisor),
            Min: min,
            Max: max,
            Divisor: divisor,
            SupportsValueCurve: p.ValueCurve,
            Options: p.Options,
            Lockable: p.Lockable);
    }

    /// <summary>
    /// The SETTINGPREFIX token (uppercase), per architecture §8a: the property's own
    /// <c>settingPrefix</c> wins when present; otherwise it is derived from <c>controlType</c>
    /// (a decimal-valued slider — <c>divisor &gt; 1</c> — serializes as <c>TEXTCTRL</c>, an
    /// integer-valued one as <c>SLIDER</c>).
    /// </summary>
    private static string ResolveSettingPrefixToken(EffectMetadataPropertyJson p, int divisor)
    {
        if (!string.IsNullOrWhiteSpace(p.SettingPrefix))
        {
            return p.SettingPrefix.ToUpperInvariant();
        }

        return p.ControlType.ToLowerInvariant() switch
        {
            "slider" => divisor > 1 ? "TEXTCTRL" : "SLIDER",
            "checkbox" => "CHECKBOX",
            "choice" or "combobox" => "CHOICE",
            "spin" => "SPIN",
            "text" => "TEXTCTRL",
            // Judgment calls (architecture §8a leaves these unspecified; none of the 56 shipped effects
            // actually use colourpicker/togglebutton, so these three are best-effort/unverified):
            "colourpicker" => "COLOUR",
            "togglebutton" => "CHECKBOX",
            "filepicker" => "FILEPICKER",
            "fontpicker" => "FONTPICKER",
            _ => p.ControlType.ToUpperInvariant(),
        };
    }

    /// <summary>
    /// Architecture §8a: <c>Rotations</c>/<c>Zoom</c> on the Buffer panel use a bare <c>SLIDER_</c>
    /// prefix with no panel-prefix letter at all, because <c>PixelBuffer</c> reads them as raw ints
    /// directly under that key. Every other property is <c>&lt;panelPrefix&gt;_&lt;SETTINGPREFIX&gt;_&lt;id&gt;</c>.
    /// </summary>
    private static string BuildSettingKey(string panelPrefix, string settingPrefixToken, string propertyId, string rawId)
    {
        if (IsBarePrefixException(rawId))
        {
            return $"{settingPrefixToken}_{propertyId}";
        }

        return $"{panelPrefix}{settingPrefixToken}_{propertyId}";
    }

    private static string BuildValueCurveSettingKey(string panelPrefix, string propertyId, string rawId)
    {
        // Same bare-prefix exception would apply to a VC on Rotations/Zoom if xLights ever supports one
        // there; not independently verified against a real file, kept consistent with the plain key rule.
        if (IsBarePrefixException(rawId))
        {
            return $"VALUECURVE_{propertyId}";
        }

        return $"{panelPrefix}VALUECURVE_{propertyId}";
    }

    private static bool IsBarePrefixException(string rawId) => rawId is "Rotations" or "Zoom";

    /// <summary>
    /// The JSON <c>default</c> is always expressed in display units (e.g. <c>1.0</c> for Zoom's
    /// "1.0x"), while xLights stores the setting as the raw int (<c>display * divisor</c>, e.g. the
    /// stored <c>SLIDER_Zoom=10</c>) — verified across every divisor&gt;1 property in the shipped
    /// metadata, including the ones whose JSON default happens to be an integer-looking token
    /// (<c>Rotations</c>'s <c>0</c>). Convert to that raw-int string so it compares equal to the raw
    /// values callers pass into <see cref="EffectSettingsSerializer"/>/<see cref="EffectSettingsValidator"/>.
    /// </summary>
    private static string? FormatDefaultValue(object? defaultValue, int divisor) =>
        defaultValue switch
        {
            null => null,
            bool b => b ? "1" : "0",
            string s => s,
            long l => divisor > 1 ? FormatRaw(l * divisor) : l.ToString(CultureInfo.InvariantCulture),
            double d => divisor > 1 ? FormatRaw(d * divisor) : FormatDecimal(d),
            _ => defaultValue.ToString(),
        };

    private static string FormatRaw(double rawValue) =>
        Math.Round(rawValue, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private static string FormatDecimal(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Architecture §8a: the legacy <c>E_</c>-prefixed keys that live on the Blending panel despite the panel's own <c>T_</c> prefix.</summary>
    private static IEnumerable<EffectPropertyVocabulary> BuildLegacyBlendingProperties()
    {
        yield return new EffectPropertyVocabulary(
            PropertyId: "LayerMorph", SettingKey: "E_CHECKBOX_LayerMorph", ValueCurveSettingKey: null,
            ControlType: "checkbox", SemanticType: "bool", DefaultValue: "0",
            Min: null, Max: null, Divisor: 1, SupportsValueCurve: false, Options: null, Lockable: false);

        // No declared min/max/default exists anywhere in the shipped metadata for this slider (it only
        // appears in prose on Blending.json's LayerMorphRow custom control) — left unconstrained rather
        // than inventing a plausible range, per the "never invent" discipline this project follows.
        yield return new EffectPropertyVocabulary(
            PropertyId: "EffectLayerMix", SettingKey: "E_SLIDER_EffectLayerMix", ValueCurveSettingKey: null,
            ControlType: "slider", SemanticType: "int", DefaultValue: null,
            Min: null, Max: null, Divisor: 1, SupportsValueCurve: false, Options: null, Lockable: false);

        // T_ copy — authoritative over the B_ copy on the Buffer panel when both are present (architecture §8a).
        // No options list is declared anywhere in the shipped metadata; left unconstrained rather than invented.
        yield return new EffectPropertyVocabulary(
            PropertyId: "LayerMethod", SettingKey: "T_CHOICE_LayerMethod", ValueCurveSettingKey: null,
            ControlType: "choice", SemanticType: "enum", DefaultValue: null,
            Min: null, Max: null, Divisor: 1, SupportsValueCurve: false, Options: null, Lockable: false);
    }

    private static EffectPropertyVocabulary BuildLayerMethodOnBuffer() =>
        new(
            PropertyId: "LayerMethod", SettingKey: "B_CHOICE_LayerMethod", ValueCurveSettingKey: null,
            ControlType: "choice", SemanticType: "enum", DefaultValue: null,
            Min: null, Max: null, Divisor: 1, SupportsValueCurve: false, Options: null, Lockable: false);

    /// <summary>
    /// Architecture §5/§8a: a palette is always eight <c>C_BUTTON_PaletteN</c> color slots plus eight
    /// <c>C_CHECKBOX_PaletteN</c> flags marking which are active — fixed, hardcoded, never mined from
    /// JSON (Color.json's own notes say so explicitly).
    /// </summary>
    private static IEnumerable<EffectPropertyVocabulary> BuildPaletteProperties()
    {
        for (var n = 1; n <= 8; n++)
        {
            yield return new EffectPropertyVocabulary(
                PropertyId: $"Palette{n}", SettingKey: $"C_BUTTON_Palette{n}", ValueCurveSettingKey: null,
                ControlType: "colourpicker", SemanticType: "color", DefaultValue: null,
                Min: null, Max: null, Divisor: 1, SupportsValueCurve: false, Options: null, Lockable: false);

            yield return new EffectPropertyVocabulary(
                PropertyId: $"PaletteCheckbox{n}", SettingKey: $"C_CHECKBOX_Palette{n}", ValueCurveSettingKey: null,
                ControlType: "checkbox", SemanticType: "bool", DefaultValue: n == 1 ? "1" : "0",
                Min: null, Max: null, Divisor: 1, SupportsValueCurve: false, Options: null, Lockable: false);
        }
    }
}
