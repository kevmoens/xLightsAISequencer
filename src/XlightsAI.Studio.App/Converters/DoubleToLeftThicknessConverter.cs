using System.Globalization;
using Avalonia.Data.Converters;

namespace XlightsAI.Studio.App.Converters;

/// <summary>Converts a plain <see cref="double"/> left-margin value (e.g. <see cref="XlightsAI.Studio.Shell.TimelineRulerTickView.MarginLeft"/>) into an Avalonia <see cref="Thickness"/>, since the Avalonia-free Shell project cannot reference Avalonia types directly.</summary>
public sealed class DoubleToLeftThicknessConverter : IValueConverter
{
    public static readonly DoubleToLeftThicknessConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Avalonia.Thickness(value is double left ? left : 0d, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
