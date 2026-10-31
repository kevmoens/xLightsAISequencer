namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class EffectSettingsSerializerTests
{
    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "effectmetadata");

    private readonly EffectVocabularyCatalog _catalog = EffectVocabularyBuilder.BuildFromDirectory(FixturesRoot);
    private readonly EffectSettingsSerializer _serializer;

    public EffectSettingsSerializerTests()
    {
        _serializer = new EffectSettingsSerializer(_catalog);
    }

    [Fact]
    public void AllDefaultEffect_SerializesToEmptyString()
    {
        var result = _serializer.Serialize("Fire", new Dictionary<string, string>
        {
            ["E_SLIDER_Fire_Height"] = "50",
            ["E_SLIDER_Fire_HueShift"] = "0",
            ["E_TEXTCTRL_Fire_GrowthCycles"] = "0",
            ["E_CHECKBOX_Fire_GrowWithMusic"] = "0",
            ["E_CHOICE_Fire_Location"] = "Bottom",
            ["E_CHOICE_Fire_Style"] = "New Render Method",
        });

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EmptySettings_SerializesToEmptyString()
    {
        Assert.Equal(string.Empty, _serializer.Serialize("On", new Dictionary<string, string>()));
    }

    [Fact]
    public void NonDefaultSettings_KeysComeOutAlphabeticallySortedRegardlessOfInputOrder()
    {
        var result = _serializer.Serialize("Fire", new Dictionary<string, string>
        {
            ["E_CHOICE_Fire_Style"] = "Old Render Method",
            ["E_CHECKBOX_Fire_GrowWithMusic"] = "1",
            ["E_SLIDER_Fire_Height"] = "75",
        });

        Assert.Equal(
            "E_CHECKBOX_Fire_GrowWithMusic=1,E_CHOICE_Fire_Style=Old Render Method,E_SLIDER_Fire_Height=75",
            result);
    }

    [Fact]
    public void ValueContainingComma_IsEscaped()
    {
        var result = _serializer.Serialize("Fire", new Dictionary<string, string>
        {
            ["E_CHOICE_Fire_Location"] = "Left,Right",
        });

        Assert.Equal("E_CHOICE_Fire_Location=Left&comma;Right", result);
    }

    [Fact]
    public void LayerMethod_TAuthoritativeOverB_DropsTheBufferCopy()
    {
        var result = _serializer.Serialize("Fire", new Dictionary<string, string>
        {
            ["T_CHOICE_LayerMethod"] = "Normal",
            ["B_CHOICE_LayerMethod"] = "Normal",
        });

        Assert.Equal("T_CHOICE_LayerMethod=Normal", result);
    }

    [Fact]
    public void LayerMethod_TOnlyPresent_StillSerializes()
    {
        var result = _serializer.Serialize("Fire", new Dictionary<string, string>
        {
            ["T_CHOICE_LayerMethod"] = "Mask",
        });

        Assert.Equal("T_CHOICE_LayerMethod=Mask", result);
    }

    [Fact]
    public void UnknownSettingKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => _serializer.Serialize("Fire", new Dictionary<string, string>
        {
            ["E_SLIDER_DoesNotExist"] = "1",
        }));
    }

    [Fact]
    public void ValueCurve_SerializesAsPipeDelimitedBlobReplacingThePlainKey()
    {
        var curve = new EffectValueCurve(CurveType: "Ramp", Min: 0, Max: 5, P1: 5, P2: 3, Reverse: true);

        var result = _serializer.Serialize(
            "Bars",
            new Dictionary<string, string>(),
            new Dictionary<string, EffectValueCurve> { ["E_VALUECURVE_Bars_BarCount"] = curve });

        Assert.Equal(
            "E_VALUECURVE_Bars_BarCount=Active=TRUE|Id=ID_VALUECURVE_Bars_BarCount|Type=Ramp|Min=0.00|Max=5.00|P1=5.00|P2=3.00|RV=TRUE|",
            result);
    }

    [Fact]
    public void ValueCurve_OmitsTypeWhenFlat()
    {
        var curve = new EffectValueCurve(CurveType: "Flat", Min: 0, Max: 100);

        var result = _serializer.Serialize(
            "Bars",
            new Dictionary<string, string>(),
            new Dictionary<string, EffectValueCurve> { ["E_VALUECURVE_Bars_BarCount"] = curve });

        Assert.Equal(
            "E_VALUECURVE_Bars_BarCount=Active=TRUE|Id=ID_VALUECURVE_Bars_BarCount|Min=0.00|Max=100.00|",
            result);
        Assert.DoesNotContain("Type=", result);
    }

    [Fact]
    public void ValueCurve_WithTimingTrackBinding_AppendsTT()
    {
        var curve = new EffectValueCurve(CurveType: "Timing Track Fade Proportional", Min: 0, Max: 100, TimingTrack: "Beats");

        var result = _serializer.Serialize(
            "Bars",
            new Dictionary<string, string>(),
            new Dictionary<string, EffectValueCurve> { ["E_VALUECURVE_Bars_BarCount"] = curve });

        Assert.Contains("TT=Beats|", result);
    }

    [Fact]
    public void ValueCurve_OnPropertyWithoutValueCurveSupport_Throws()
    {
        var curve = new EffectValueCurve(CurveType: "Ramp", Min: 0, Max: 1);

        Assert.Throws<ArgumentException>(() => _serializer.Serialize(
            "Bars",
            new Dictionary<string, string>(),
            new Dictionary<string, EffectValueCurve> { ["E_VALUECURVE_Bars_Direction"] = curve }));
    }
}
