namespace XlightsAI.Studio.Pipeline;

public sealed record ExemplarGesture(
    string Id, string Role, string Effect, double OnsetBeats, double DurationBeats,
    int Layer, bool ContinuesIn, bool ContinuesOut, double? CadenceBeats,
    IReadOnlyList<string> Overlaps);

/// <summary>Interpolates between measured beats, extrapolating only at the two grid edges.</summary>
public sealed class ExemplarBeatAxis(IEnumerable<long> ticks)
{
    private readonly long[] _ticks = ticks.Distinct().Order().ToArray();
    public bool IsUsable => _ticks.Length >= 2;

    public double BeatAt(long ticks)
    {
        var index = Array.BinarySearch(_ticks, ticks);
        if (index >= 0)
        {
            return index;
        }
        index = Math.Clamp(~index - 1, 0, _ticks.Length - 2);
        return index + (ticks - _ticks[index]) / (double)(_ticks[index + 1] - _ticks[index]);
    }

    public long TicksAt(double beat)
    {
        var index = Math.Clamp((int)Math.Floor(beat), 0, _ticks.Length - 2);
        return _ticks[index] + (long)Math.Round((beat - index) * (_ticks[index + 1] - _ticks[index]));
    }
}
