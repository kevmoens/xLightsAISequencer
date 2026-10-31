using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XlightsAI.Studio.App.Converters;

/// <summary>
/// Picks one note block's fill from its <c>PianoRollNoteRowView</c> flags, in priority order —
/// selection is the loudest signal (you are about to act on this note), then locked (editing it
/// will be rejected), then low-confidence (worth a second look), then user-corrected (already
/// handled), then a plain default for an ordinary, unremarkable note. A note can be more than one
/// of these at once; only the highest-priority color shows, same "one color wins" simplicity as
/// <see cref="EffectBlockBrushConverter"/>.
/// </summary>
public sealed class NoteBrushConverter : IMultiValueConverter
{
    public static readonly NoteBrushConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = values.Count > 0 && values[0] is true;
        var isLocked = values.Count > 1 && values[1] is true;
        var isLowConfidence = values.Count > 2 && values[2] is true;
        var isUserCorrected = values.Count > 3 && values[3] is true;

        if (isSelected)
        {
            return Brushes.Gold;
        }

        if (isLocked)
        {
            return Brushes.DarkRed;
        }

        if (isLowConfidence)
        {
            return Brushes.OrangeRed;
        }

        if (isUserCorrected)
        {
            return Brushes.MediumSeaGreen;
        }

        return Brushes.SteelBlue;
    }
}
