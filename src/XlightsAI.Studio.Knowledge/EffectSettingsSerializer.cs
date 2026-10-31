using System.Globalization;
using System.Text;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// PHASE-2a: compiles a settings dictionary into the single flat comma-separated <c>key=value</c>
/// string xLights writes inside an <c>&lt;Effect&gt;</c> element's settings, following every
/// serialization rule architecture §8a lists and verified against real files:
/// <list type="bullet">
/// <item>defaults are omitted entirely (an all-default effect serializes to the empty string);</item>
/// <item>keys are written in alphabetical sort order;</item>
/// <item>commas inside a value are escaped as <c>&amp;comma;</c> — done here, independently of any
/// XML-layer escaping <c>XsqExporter</c> performs, since that is a distinct bug class;</item>
/// <item>a value curve on a property replaces its plain key with the <c>VALUECURVE</c> key and a
/// pipe-delimited blob;</item>
/// <item><c>LayerMethod</c>'s <c>T_</c> copy is authoritative over its <c>B_</c> copy — the <c>B_</c>
/// copy is dropped when both are present.</item>
/// </list>
/// </summary>
public sealed class EffectSettingsSerializer
{
    private const string LayerMethodAuthoritativeKey = "T_CHOICE_LayerMethod";
    private const string LayerMethodSubordinateKey = "B_CHOICE_LayerMethod";

    private readonly EffectVocabularyCatalog _catalog;
    private readonly EffectSettingsValidator _validator;

    public EffectSettingsSerializer(EffectVocabularyCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _validator = new EffectSettingsValidator(catalog);
    }

    /// <summary>
    /// Serializes <paramref name="plainSettings"/> (keyed by fully-qualified setting key, e.g.
    /// <c>E_SLIDER_Bars_BarCount</c>) and <paramref name="valueCurves"/> (keyed by that same property's
    /// <see cref="EffectPropertyVocabulary.ValueCurveSettingKey"/>) into one comma-joined settings
    /// string. Throws for any key the vocabulary does not recognize for <paramref name="effectName"/> —
    /// callers are expected to have already validated via <see cref="EffectSettingsValidator"/>.
    /// </summary>
    public string Serialize(
        string effectName,
        IReadOnlyDictionary<string, string> plainSettings,
        IReadOnlyDictionary<string, EffectValueCurve>? valueCurves = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectName);
        ArgumentNullException.ThrowIfNull(plainSettings);

        var entries = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in plainSettings)
        {
            if (!_catalog.TryGetProperty(effectName, key, out var property))
            {
                throw new ArgumentException($"'{key}' is not a known setting for effect '{effectName}'.", nameof(plainSettings));
            }

            if (string.Equals(value, property.DefaultValue, StringComparison.Ordinal))
            {
                continue;
            }

            entries[key] = EscapeCommas(value);
        }

        if (valueCurves is not null)
        {
            foreach (var (key, curve) in valueCurves)
            {
                if (!_validator.IsSupportedValueCurve(effectName, key))
                {
                    throw new ArgumentException(
                        $"'{key}' is not a value-curve-capable setting for effect '{effectName}'.", nameof(valueCurves));
                }

                _catalog.TryGetProperty(effectName, key, out var property);
                entries[key] = EscapeCommas(FormatValueCurve(property.PropertyId, curve));
            }
        }

        // Architecture §8a: LayerMethod appears under both T_ (Blending) and B_ (Buffer); T_ is
        // authoritative, so a redundant B_ copy never reaches the serialized string.
        if (entries.ContainsKey(LayerMethodAuthoritativeKey))
        {
            entries.Remove(LayerMethodSubordinateKey);
        }

        return string.Join(",", entries.Select(e => $"{e.Key}={e.Value}"));
    }

    private static string EscapeCommas(string value) => value.Replace(",", "&comma;", StringComparison.Ordinal);

    /// <summary>
    /// Architecture §8a's pipe-delimited value-curve blob:
    /// <c>Active=TRUE|Id=ID_VALUECURVE_&lt;Name&gt;|Type=&lt;Type&gt;|Min=...|Max=...|P1=...|P2=...|RV=TRUE|</c>,
    /// with <c>Type=</c> omitted when the curve is <c>Flat</c> and <c>TT=</c>/<c>AT=</c>/<c>FT=</c>
    /// appended when the curve binds to a timing track, audio track, or filter label.
    /// </summary>
    private static string FormatValueCurve(string propertyId, EffectValueCurve curve)
    {
        var blob = new StringBuilder();
        blob.Append("Active=TRUE|");
        blob.Append("Id=ID_VALUECURVE_").Append(propertyId).Append('|');

        if (!string.Equals(curve.CurveType, "Flat", StringComparison.Ordinal))
        {
            blob.Append("Type=").Append(curve.CurveType).Append('|');
        }

        blob.Append("Min=").Append(FormatNumber(curve.Min)).Append('|');
        blob.Append("Max=").Append(FormatNumber(curve.Max)).Append('|');

        if (curve.P1 is { } p1)
        {
            blob.Append("P1=").Append(FormatNumber(p1)).Append('|');
        }

        if (curve.P2 is { } p2)
        {
            blob.Append("P2=").Append(FormatNumber(p2)).Append('|');
        }

        if (curve.Reverse)
        {
            blob.Append("RV=TRUE|");
        }

        if (curve.TimingTrack is { Length: > 0 } tt)
        {
            blob.Append("TT=").Append(tt).Append('|');
        }

        if (curve.AudioTrack is { Length: > 0 } at)
        {
            blob.Append("AT=").Append(at).Append('|');
        }

        if (curve.FilterLabel is { Length: > 0 } ft)
        {
            blob.Append("FT=").Append(ft).Append('|');
        }

        return blob.ToString();
    }

    private static string FormatNumber(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
