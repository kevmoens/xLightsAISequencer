namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class EffectVocabularyBuilderTests
{
    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "effectmetadata");

    private static EffectVocabularyCatalog BuildFixtureCatalog() => EffectVocabularyBuilder.BuildFromDirectory(FixturesRoot);

    [Fact]
    public void BuildFromDirectory_ParsesEveryEffectFileExceptSchema()
    {
        var catalog = BuildFixtureCatalog();

        Assert.Equal(5, catalog.Effects.Count);
        Assert.Contains("On", catalog.Effects.Keys);
        Assert.Contains("Bars", catalog.Effects.Keys);
        Assert.Contains("Fire", catalog.Effects.Keys);
        Assert.Contains("Faces", catalog.Effects.Keys);
        Assert.Contains("Text", catalog.Effects.Keys);
    }

    [Fact]
    public void BuildFromDirectory_Faces_MouthMovementsSurvivesTheCustomControlTypeFilter()
    {
        // Build-plan Phase 8 task 3: Faces_MouthMovements is "controlType": "custom" in the real shipped
        // metadata (it would otherwise be silently dropped like every other custom property), so this
        // asserts the documented hardcoded exception actually keeps it available for timing-track binding.
        var catalog = BuildFixtureCatalog();

        var property = catalog.Effects["Faces"].FindByPropertyId("Faces_MouthMovements");

        Assert.NotNull(property);
        Assert.Equal("E_CHOICE_Faces_MouthMovements", property!.SettingKey);
    }

    [Fact]
    public void IntegerSlider_WithoutDivisor_UsesSliderSettingPrefix()
    {
        var catalog = BuildFixtureCatalog();
        var property = catalog.Effects["Bars"].FindByPropertyId("Bars_BarCount");

        Assert.NotNull(property);
        Assert.Equal("E_SLIDER_Bars_BarCount", property!.SettingKey);
        Assert.Equal("1", property.DefaultValue);
        Assert.Equal("E_VALUECURVE_Bars_BarCount", property.ValueCurveSettingKey);
    }

    [Fact]
    public void DecimalSlider_WithDivisor_UsesTextCtrlSettingPrefixAndRawScaledDefault()
    {
        var catalog = BuildFixtureCatalog();
        var property = catalog.Effects["Bars"].FindByPropertyId("Bars_Cycles");

        Assert.NotNull(property);
        Assert.Equal("E_TEXTCTRL_Bars_Cycles", property!.SettingKey);
        // default 1.0 display * divisor 10 = raw stored "10" (architecture §8a: decimal sliders serialize under TEXTCTRL).
        Assert.Equal("10", property.DefaultValue);
    }

    [Fact]
    public void ExplicitSettingPrefix_OverridesDerivedOne()
    {
        var catalog = BuildFixtureCatalog();
        // Bars_Center is an integer slider (no divisor) but its JSON declares settingPrefix:"TEXTCTRL" explicitly.
        var property = catalog.Effects["Bars"].FindByPropertyId("Bars_Center");

        Assert.NotNull(property);
        Assert.Equal("E_TEXTCTRL_Bars_Center", property!.SettingKey);
    }

    [Fact]
    public void Checkbox_SerializesBoolDefaultAsOneOrZero()
    {
        var catalog = BuildFixtureCatalog();
        var property = catalog.Effects["Bars"].FindByPropertyId("Bars_Highlight");

        Assert.NotNull(property);
        Assert.Equal("E_CHECKBOX_Bars_Highlight", property!.SettingKey);
        Assert.Equal("0", property.DefaultValue);
    }

    [Fact]
    public void Choice_CarriesItsOptionsAndKeyedUnderChoice()
    {
        var catalog = BuildFixtureCatalog();
        var property = catalog.Effects["Fire"].FindByPropertyId("Fire_Location");

        Assert.NotNull(property);
        Assert.Equal("E_CHOICE_Fire_Location", property!.SettingKey);
        Assert.Equal("Bottom", property.DefaultValue);
        Assert.Equal(["Bottom", "Top", "Left", "Right"], property.Options);
    }

    [Fact]
    public void PropertyWithoutValueCurve_HasNoValueCurveSettingKey()
    {
        var catalog = BuildFixtureCatalog();
        var property = catalog.Effects["Fire"].FindByPropertyId("Fire_Location");

        Assert.NotNull(property);
        Assert.False(property!.SupportsValueCurve);
        Assert.Null(property.ValueCurveSettingKey);
    }

    [Fact]
    public void BufferPanel_RotationsAndZoom_UseBarePrefixWithNoPanelLetter()
    {
        var catalog = BuildFixtureCatalog();
        var rotations = catalog.Buffer.FindByPropertyId("Rotations");
        var zoom = catalog.Buffer.FindByPropertyId("Zoom");

        Assert.NotNull(rotations);
        Assert.NotNull(zoom);
        Assert.Equal("SLIDER_Rotations", rotations!.SettingKey);
        Assert.Equal("SLIDER_Zoom", zoom!.SettingKey);
        // default 1.0 display * divisor 10 = raw "10" (Buffer.json's own note: "10 = 1.0x").
        Assert.Equal("10", zoom.DefaultValue);
    }

    [Fact]
    public void BufferPanel_SubBufferCustomControl_IsExcludedDespiteBeingSerializedByXlights()
    {
        var catalog = BuildFixtureCatalog();

        // Judgment call (see EffectVocabularyBuilder): all controlType:"custom" rows are excluded from
        // the generated vocabulary, even the one (SubBuffer) whose description says it IS serialized
        // (as a free-form "x1 y1 x2 y2" string with no numeric/enum validation semantics worth modeling).
        Assert.Null(catalog.Buffer.FindByPropertyId("SubBuffer"));
        Assert.Null(catalog.Buffer.FindByPropertyId("ResetPanelRow"));
    }

    [Fact]
    public void LegacyLayerMethod_IsPresentOnBothPanelsWithDistinctKeys()
    {
        var catalog = BuildFixtureCatalog();
        var onBlending = catalog.Blending.FindByPropertyId("LayerMethod");
        var onBuffer = catalog.Buffer.FindByPropertyId("LayerMethod");

        Assert.NotNull(onBlending);
        Assert.NotNull(onBuffer);
        Assert.Equal("T_CHOICE_LayerMethod", onBlending!.SettingKey);
        Assert.Equal("B_CHOICE_LayerMethod", onBuffer!.SettingKey);
    }

    [Fact]
    public void LegacyLayerMorphAndEffectLayerMix_KeepTheirELegacyKeysOnTheBlendingPanel()
    {
        var catalog = BuildFixtureCatalog();
        var morph = catalog.Blending.FindByPropertyId("LayerMorph");
        var mix = catalog.Blending.FindByPropertyId("EffectLayerMix");

        Assert.NotNull(morph);
        Assert.NotNull(mix);
        Assert.Equal("E_CHECKBOX_LayerMorph", morph!.SettingKey);
        Assert.Equal("E_SLIDER_EffectLayerMix", mix!.SettingKey);
    }

    [Fact]
    public void ColorPanel_Has16HardcodedPaletteSlots()
    {
        var catalog = BuildFixtureCatalog();

        for (var n = 1; n <= 8; n++)
        {
            var button = catalog.Color.FindByPropertyId($"Palette{n}");
            var checkbox = catalog.Color.FindByPropertyId($"PaletteCheckbox{n}");

            Assert.NotNull(button);
            Assert.NotNull(checkbox);
            Assert.Equal($"C_BUTTON_Palette{n}", button!.SettingKey);
            Assert.Equal($"C_CHECKBOX_Palette{n}", checkbox!.SettingKey);
        }
    }
}
