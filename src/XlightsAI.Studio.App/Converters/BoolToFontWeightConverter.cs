using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XlightsAI.Studio.App.Converters;

/// <summary>PHASE-03 Step 017: bolds a group row's label so a prop row's group hierarchy reads visually distinct from leaf model rows, without any Avalonia type leaking into the Shell project.</summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.Bold : FontWeight.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
