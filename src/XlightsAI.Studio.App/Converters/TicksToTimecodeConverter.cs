using System.Globalization;
using Avalonia.Data.Converters;

namespace XlightsAI.Studio.App.Converters;

/// <summary>
/// Renders a 100-nanosecond tick count as a human timecode (<c>m:ss.mmm</c>, or <c>h:mm:ss.mmm</c>
/// past an hour).
/// <para>
/// The tick is the right storage and transport unit — it is what the sequence format, the audio
/// decoder and the viewport transform all agree on, and rounding it for display must never round
/// it in the model. What was wrong was showing it raw: every time readout and every time *input*
/// in the shell was labelled "(100 ns ticks)", so reviewing a sequence meant reading
/// <c>1,234,500,000</c> and dividing by ten million in your head. This converter is display-only
/// and one-way; the bound view-model property keeps its exact tick value.
/// </para>
/// </summary>
public sealed class TicksToTimecodeConverter : IValueConverter
{
    public static readonly TicksToTimecodeConverter Instance = new();

    private const long TicksPerSecond = 10_000_000L;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryGetTicks(value, out var ticks))
        {
            return "—";
        }

        var span = TimeSpan.FromTicks(ticks);
        var sign = ticks < 0 ? "-" : string.Empty;
        span = span.Duration();

        var text = span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}"
            : $"{span.Minutes}:{span.Seconds:00}.{span.Milliseconds:000}";

        // The exact tick value stays one hover away rather than being lost: a reviewer comparing a
        // boundary against the sequence file still needs it, they just no longer need it to read
        // the screen.
        return parameter as string == "WithTicks"
            ? $"{sign}{text}  ({ticks:N0} ticks)"
            : sign + text;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException(
            $"{nameof(TicksToTimecodeConverter)} is display-only. A two-way time field must bind the "
            + "view model's own tick property so the exact authored value is never rounded by the view.");

    private static bool TryGetTicks(object? value, out long ticks)
    {
        switch (value)
        {
            case long l:
                ticks = l;
                return true;
            case int i:
                ticks = i;
                return true;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                ticks = (long)d;
                return true;
            case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                ticks = parsed;
                return true;
            default:
                ticks = 0;
                return false;
        }
    }

    /// <summary>
    /// Seconds-per-tick, exposed so a caller that needs the scale can use the same constant this
    /// converter formats with instead of re-deriving <c>10_000_000</c> at its own call site.
    /// </summary>
    public static long TicksInOneSecond => TicksPerSecond;
}
