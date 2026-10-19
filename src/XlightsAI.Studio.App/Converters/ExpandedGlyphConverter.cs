using System.Globalization;
using Avalonia.Data.Converters;

namespace XlightsAI.Studio.App.Converters;

/// <summary>PHASE-03 Step 017: renders a group row's <see cref="XlightsAI.Studio.Shell.TimelineRowItemView.IsExpanded"/> as a plain text expand/collapse glyph for its toggle button.</summary>
public sealed class ExpandedGlyphConverter : IValueConverter
{
    public static readonly ExpandedGlyphConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "▾" : "▸"; // ▾ expanded, ▸ collapsed

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
