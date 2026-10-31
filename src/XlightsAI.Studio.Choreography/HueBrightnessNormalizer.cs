namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 3 ("Helmholtz-Kohlrausch per-hue brightness normalization"). Architecture
/// §7: "for narrow-band LEDs, saturated red reads roughly 5x brighter than green and blue ~2.5x
/// brighter than green at equal drive... RGB(255,0,0), (0,255,0) and (0,0,255) do not look equally
/// bright." This is a disclosed, deliberately simplified model of the real Helmholtz-Kohlrausch effect
/// (a full CIE perceptual-brightness model needs spectral LED data this codebase does not have) --
/// three measured anchor weights (red=5.0, green=1.0 baseline, blue=2.5) interpolated by hue angle
/// around the wheel, applied to a color's own HSV saturation/value. Good enough to equalize a chase's
/// perceived pulse and to "exploit it... saturated blue at low drive is cheap and still reads as lit"
/// (architecture §7) -- not a scientific-grade colorimetry engine.
/// </summary>
public static class HueBrightnessNormalizer
{
    /// <summary>(hue degrees, perceived-brightness weight relative to green=1.0) anchors from architecture §7, with red repeated at 360° to close the wheel.</summary>
    private static readonly (double HueDegrees, double Weight)[] Anchors =
    [
        (0, 5.0), (120, 1.0), (240, 2.5), (360, 5.0),
    ];

    /// <summary>The perceived-brightness weight at <paramref name="hueDegrees"/> (any value, wrapped into <c>[0,360)</c>), linearly interpolated between the nearest two anchors.</summary>
    public static double WeightForHue(double hueDegrees)
    {
        var h = ((hueDegrees % 360) + 360) % 360;

        for (var i = 0; i < Anchors.Length - 1; i++)
        {
            var (h0, w0) = Anchors[i];
            var (h1, w1) = Anchors[i + 1];
            if (h >= h0 && h <= h1)
            {
                var t = (h - h0) / (h1 - h0);
                return w0 + ((w1 - w0) * t);
            }
        }

        return 1.0;
    }

    /// <summary>
    /// This color's own perceived brightness relative to an equal-nominal-value neutral grey: HSV value
    /// scaled by a saturation-weighted hue multiplier (fully desaturated -- white/grey/black -- carries
    /// no hue bias, weight 1.0; fully saturated carries the full <see cref="WeightForHue"/> multiplier).
    /// </summary>
    public static double PerceivedBrightness(byte r, byte g, byte b)
    {
        var (hue, saturation, value) = ToHsv(r, g, b);
        var weight = 1.0 + (saturation * (WeightForHue(hue) - 1.0));
        return value * weight;
    }

    /// <summary>
    /// Scales <paramref name="r"/>/<paramref name="g"/>/<paramref name="b"/>'s own HSV *value* (hue and
    /// saturation unchanged) so <see cref="PerceivedBrightness"/> of the result equals
    /// <paramref name="targetPerceivedBrightness"/> (clamped into a representable range) -- the
    /// correction architecture §7 calls for so "an R-to-G-to-B chase at constant nominal value" stops
    /// visibly pulsing.
    /// </summary>
    public static (byte R, byte G, byte B) NormalizeToPerceivedBrightness(byte r, byte g, byte b, double targetPerceivedBrightness)
    {
        var (hue, saturation, value) = ToHsv(r, g, b);
        var weight = 1.0 + (saturation * (WeightForHue(hue) - 1.0));

        if (weight <= 0 || value <= 0)
        {
            return (r, g, b);
        }

        var targetValue = Math.Clamp(targetPerceivedBrightness / weight, 0.0, 1.0);
        return FromHsv(hue, saturation, targetValue);
    }

    private static (double Hue, double Saturation, double Value) ToHsv(byte r, byte g, byte b)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        double hue;
        if (delta <= 0)
        {
            hue = 0;
        }
        else if (max == rf)
        {
            hue = 60 * (((gf - bf) / delta) % 6);
        }
        else if (max == gf)
        {
            hue = 60 * (((bf - rf) / delta) + 2);
        }
        else
        {
            hue = 60 * (((rf - gf) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max <= 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static (byte R, byte G, byte B) FromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60 % 2) - 1));
        var m = value - c;

        var (rf, gf, bf) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return (
            (byte)Math.Clamp(Math.Round((rf + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((gf + m) * 255), 0, 255),
            (byte)Math.Clamp(Math.Round((bf + m) * 255), 0, 255));
    }
}
