using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XlightsAI.Studio.App.Converters;

/// <summary>
/// PHASE-03 Step 018: picks an effect block's background brush from its
/// <see cref="XlightsAI.Studio.Shell.EffectBlockItemView.IsLocked"/>/<see cref="XlightsAI.Studio.Shell.EffectBlockItemView.IsSelected"/>
/// state — locked is visually distinct (not merely inspector text) so a user does not discover a
/// lock only after a rejected edit attempt, and a selected block is visually distinct so "which
/// effect is selected" is never ambiguous on screen.
/// </summary>
public sealed class EffectBlockBrushConverter : IValueConverter
{
    public static readonly EffectBlockBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool flag || !flag)
        {
            return parameter as string == "Locked" ? Brushes.SteelBlue : Brushes.Transparent;
        }

        return (parameter as string) switch
        {
            "Locked" => Brushes.DarkRed,
            "Selected" => Brushes.Gold,
            _ => Brushes.Transparent,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
