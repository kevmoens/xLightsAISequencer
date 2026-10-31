namespace XlightsAI.Studio.Knowledge.Tests;

public sealed class EffectSettingsValidatorTests
{
    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "Fixtures", "effectmetadata");

    private readonly EffectVocabularyCatalog _catalog = EffectVocabularyBuilder.BuildFromDirectory(FixturesRoot);
    private readonly EffectSettingsValidator _validator;

    public EffectSettingsValidatorTests()
    {
        _validator = new EffectSettingsValidator(_catalog);
    }

    [Fact]
    public void RejectsUnknownPropertyName()
    {
        Assert.False(_validator.IsSupportedParameterValue("Bars", "E_SLIDER_DoesNotExist", "1"));
    }

    [Fact]
    public void AcceptsKnownPropertyWithinRange()
    {
        Assert.True(_validator.IsSupportedParameterValue("Bars", "E_SLIDER_Bars_BarCount", "3"));
    }

    [Fact]
    public void RejectsOutOfRangeNumericValue()
    {
        // Bars_BarCount's declared max is 5.
        Assert.False(_validator.IsSupportedParameterValue("Bars", "E_SLIDER_Bars_BarCount", "6"));
    }

    [Fact]
    public void RejectsBelowMinimumNumericValue()
    {
        // Bars_BarCount's declared min is 1.
        Assert.False(_validator.IsSupportedParameterValue("Bars", "E_SLIDER_Bars_BarCount", "0"));
    }

    [Fact]
    public void AcceptsValidEnumChoice()
    {
        Assert.True(_validator.IsSupportedParameterValue("Bars", "E_CHOICE_Bars_Direction", "up"));
    }

    [Fact]
    public void RejectsInvalidEnumChoice()
    {
        Assert.False(_validator.IsSupportedParameterValue("Bars", "E_CHOICE_Bars_Direction", "Sideways"));
    }

    [Fact]
    public void AcceptsValueCurveOnAPropertyThatSupportsOne()
    {
        // Bars_BarCount declares "valueCurve": true.
        Assert.True(_validator.IsSupportedValueCurve("Bars", "E_VALUECURVE_Bars_BarCount"));
    }

    [Fact]
    public void RejectsValueCurveOnAPropertyThatDoesNotSupportOne()
    {
        // Bars_Direction has no "valueCurve" flag at all.
        Assert.False(_validator.IsSupportedValueCurve("Bars", "E_VALUECURVE_Bars_Direction"));
    }

    [Fact]
    public void RejectsValueCurveKeyUsedAsAPlainValue()
    {
        Assert.False(_validator.IsSupportedParameterValue("Bars", "E_VALUECURVE_Bars_BarCount", "3"));
    }

    [Fact]
    public void SharedBufferPanelProperty_ValidatesForAnyEffect()
    {
        Assert.True(_validator.IsSupportedParameterValue("Fire", "B_SLIDER_Blur", "5"));
        Assert.False(_validator.IsSupportedParameterValue("Fire", "B_SLIDER_Blur", "20"));
    }

    [Fact]
    public void ValidateSettings_ReturnsOneErrorPerViolationWithTheCorrectKind()
    {
        var errors = _validator.ValidateSettings("Bars", new Dictionary<string, string>
        {
            ["E_SLIDER_Bars_BarCount"] = "3", // valid, should not appear
            ["E_SLIDER_DoesNotExist"] = "1",
            ["E_SLIDER_Bars_BarCount_Also_Missing"] = "1",
            ["E_CHOICE_Bars_Direction"] = "Sideways",
        });

        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, e => e.Key == "E_SLIDER_DoesNotExist" && e.Kind == EffectSettingValidationErrorKind.UnknownProperty);
        Assert.Contains(errors, e => e.Key == "E_CHOICE_Bars_Direction" && e.Kind == EffectSettingValidationErrorKind.InvalidChoice);
    }

    [Fact]
    public void ValidateSettings_FlagsOutOfRangeNumeric()
    {
        var errors = _validator.ValidateSettings("Bars", new Dictionary<string, string>
        {
            ["E_SLIDER_Bars_BarCount"] = "99",
        });

        var error = Assert.Single(errors);
        Assert.Equal(EffectSettingValidationErrorKind.OutOfRange, error.Kind);
    }
}
