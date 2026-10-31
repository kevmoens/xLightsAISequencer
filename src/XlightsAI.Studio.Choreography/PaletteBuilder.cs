using System.Globalization;
using XlightsAI.Studio.Xlights;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Architecture §5: "a palette is always eight C_BUTTON_PaletteN slots plus C_CHECKBOX_PaletteN=1
/// marking which are active... palette discipline is expressed by which checkboxes are ticked, not by
/// which buttons are set." Builds a verified <see cref="ColorPaletteDefinition.RawSettings"/> string from
/// a declared color list (format cross-checked against real multi-colour palettes in
/// <c>C:\xLights</c>'s own <c>.xsq</c> files).
/// </summary>
public static class PaletteBuilder
{
    /// <summary>
    /// Builds the 8-slot palette string: the first <paramref name="activeColorsHex"/> colors fill slots
    /// 1..N and are checked active; remaining slots (up to 8) repeat the last declared color unchecked
    /// (real xLights palettes always carry 8 real button values, never fewer -- see class doc comment).
    /// </summary>
    public static ColorPaletteDefinition Build(IReadOnlyList<string> activeColorsHex)
    {
        ArgumentNullException.ThrowIfNull(activeColorsHex);
        if (activeColorsHex.Count is < 1 or > 8)
        {
            throw new ArgumentException("A palette needs between 1 and 8 active colors.", nameof(activeColorsHex));
        }

        foreach (var hex in activeColorsHex)
        {
            if (!IsValidHex(hex))
            {
                throw new ArgumentException($"'{hex}' is not a valid '#RRGGBB' color.", nameof(activeColorsHex));
            }
        }

        var entries = new List<string>(16);
        for (var slot = 1; slot <= 8; slot++)
        {
            var color = slot <= activeColorsHex.Count ? activeColorsHex[slot - 1] : activeColorsHex[^1];
            entries.Add($"C_BUTTON_Palette{slot}={color}");
        }

        for (var slot = 1; slot <= activeColorsHex.Count; slot++)
        {
            entries.Add($"C_CHECKBOX_Palette{slot}=1");
        }

        return new ColorPaletteDefinition(string.Join(",", entries));
    }

    private static bool IsValidHex(string value) =>
        value.Length == 7 && value[0] == '#' && int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
}
