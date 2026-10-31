namespace XlightsAI.Studio.Choreography.Tests;

/// <summary>Build-plan Phase 4 task 3 ("Helmholtz-Kohlrausch per-hue brightness normalization"): "saturated red reads roughly 5x brighter than green and blue ~2.5x brighter than green at equal drive."</summary>
public sealed class HueBrightnessNormalizerTests
{
    [Fact]
    public void PerceivedBrightness_PureRedGreenBlueAtEqualNominalValue_AreNotEqual()
    {
        var red = HueBrightnessNormalizer.PerceivedBrightness(255, 0, 0);
        var green = HueBrightnessNormalizer.PerceivedBrightness(0, 255, 0);
        var blue = HueBrightnessNormalizer.PerceivedBrightness(0, 0, 255);

        Assert.True(red > blue, "Red should read brighter than blue at equal nominal value.");
        Assert.True(blue > green, "Blue should read brighter than green at equal nominal value.");

        // Architecture §7's own measured ratios: red ~5x green, blue ~2.5x green.
        Assert.Equal(5.0, red / green, precision: 3);
        Assert.Equal(2.5, blue / green, precision: 3);
    }

    [Fact]
    public void PerceivedBrightness_DesaturatedColors_AreUnaffectedByHue()
    {
        var white = HueBrightnessNormalizer.PerceivedBrightness(255, 255, 255);
        var grey = HueBrightnessNormalizer.PerceivedBrightness(128, 128, 128);
        var black = HueBrightnessNormalizer.PerceivedBrightness(0, 0, 0);

        Assert.Equal(1.0, white, precision: 3);
        Assert.Equal(128 / 255.0, grey, precision: 3);
        Assert.Equal(0.0, black, precision: 3);
    }

    [Fact]
    public void NormalizeToPerceivedBrightness_RedAndGreen_ReadEquallyBright_AfterNormalization()
    {
        var targetBrightness = HueBrightnessNormalizer.PerceivedBrightness(0, 255, 0); // green's own full-value brightness.

        var (r, g, b) = HueBrightnessNormalizer.NormalizeToPerceivedBrightness(255, 0, 0, targetBrightness);
        var normalizedRedBrightness = HueBrightnessNormalizer.PerceivedBrightness(r, g, b);

        Assert.Equal(targetBrightness, normalizedRedBrightness, precision: 2);
        // Hue is preserved -- still pure red, just dimmer.
        Assert.Equal(0, g);
        Assert.Equal(0, b);
        Assert.True(r < 255, "Red's own value must have been scaled down to match green's lower perceived brightness.");
    }
}
