using System.Text.Json.Serialization;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Raw deserialization shape of one <c>resources/effectmetadata/&lt;Effect&gt;.json</c> file (or one of
/// the shared <c>Buffer.json</c>/<c>Blending.json</c>/<c>Color.json</c> panels), matching
/// <c>effectmetadata/_schema.json</c> exactly. This is the wire format only — <see cref="EffectVocabularyBuilder"/>
/// turns it into the processed <see cref="EffectVocabulary"/> model the rest of this project uses.
/// </summary>
internal sealed class EffectMetadataFileJson
{
    public string EffectName { get; set; } = string.Empty;

    /// <summary>Only present on the shared panel files (<c>B_</c>/<c>T_</c>/<c>C_</c>); absent (defaults to <c>E_</c>) on a real effect file.</summary>
    public string? Prefix { get; set; }

    public List<EffectMetadataPropertyJson> Properties { get; set; } = [];
}

/// <summary>One <c>property</c> entry from <c>_schema.json</c>'s <c>definitions.property</c>.</summary>
internal sealed class EffectMetadataPropertyJson
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Semantic type: <c>int</c>, <c>float</c>, <c>bool</c>, <c>enum</c>, <c>string</c>, <c>color</c>, <c>file</c>, <c>font</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>UI control type: <c>slider</c>, <c>checkbox</c>, <c>choice</c>, <c>combobox</c>, <c>spin</c>, <c>text</c>, <c>filepicker</c>, <c>fontpicker</c>, <c>colourpicker</c>, <c>togglebutton</c>, <c>point2d</c>, <c>custom</c>.</summary>
    public string ControlType { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonElementBoxingConverter))]
    public object? Default { get; set; }

    public double? Min { get; set; }

    public double? Max { get; set; }

    /// <summary>1 = int, 10 = one decimal place, 100 = two decimal places, 360 = degrees-as-int, etc. Absent means 1 (integer-valued).</summary>
    public int? Divisor { get; set; }

    public bool ValueCurve { get; set; }

    public bool Lockable { get; set; }

    public List<string>? Options { get; set; }

    public bool SuppressIfDefault { get; set; }

    /// <summary>Explicit override of the derived SETTINGPREFIX token (e.g. <c>"TEXTCTRL"</c>, <c>"SLIDER"</c>, <c>"CHOICE"</c>).</summary>
    public string? SettingPrefix { get; set; }

    public double? MinX { get; set; }

    public double? MaxX { get; set; }

    [JsonConverter(typeof(JsonElementBoxingConverter))]
    public object? DefaultX { get; set; }

    public double? MinY { get; set; }

    public double? MaxY { get; set; }

    [JsonConverter(typeof(JsonElementBoxingConverter))]
    public object? DefaultY { get; set; }

    public string? Description { get; set; }
}
