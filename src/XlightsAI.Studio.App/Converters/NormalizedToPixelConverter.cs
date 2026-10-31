using System.Globalization;
using Avalonia.Data.Converters;

namespace XlightsAI.Studio.App.Converters;

/// <summary>
/// Scales a normalized <c>[0,1]</c> coordinate (e.g. <c>DisplayNodeRowView.X</c>/<c>Y</c>) by a fixed
/// pixel extent passed as the converter parameter, for positioning a node on a fixed-size
/// <see cref="Avalonia.Controls.Canvas"/> via <c>Canvas.Left</c>/<c>Canvas.Top</c>.
/// </summary>
public sealed class NormalizedToPixelConverter : IValueConverter
{
    public static readonly NormalizedToPixelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double normalized || parameter is not string extentText
            || !double.TryParse(extentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var extent))
        {
            return 0d;
        }

        return normalized * extent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException($"{nameof(NormalizedToPixelConverter)} is one-way (display positioning only).");
}
