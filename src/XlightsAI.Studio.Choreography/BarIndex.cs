namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 4 task 6 ("typed BarIndex/BeatIndex/Ticks throughout, explicit key threading --
/// the architecture doc flags a real historical bug class here"): a zero-based bar number, distinct at
/// the type level from a raw <see langword="int"/> or a <see cref="BeatIndex"/>, so a bar index can
/// never be silently used where a beat index (or a millisecond, or a tick count) was meant -- the exact
/// shape of bug <c>CLAUDE.md</c> documents ("building a per-section lookup key in milliseconds against a
/// table keyed by second-floats... one prop group took 74% of the show").
/// </summary>
public readonly record struct BarIndex(int Value)
{
    public static BarIndex operator +(BarIndex left, int right) => new(left.Value + right);

    public static int operator -(BarIndex left, BarIndex right) => left.Value - right.Value;

    public static bool operator <(BarIndex left, BarIndex right) => left.Value < right.Value;

    public static bool operator >(BarIndex left, BarIndex right) => left.Value > right.Value;

    public static bool operator <=(BarIndex left, BarIndex right) => left.Value <= right.Value;

    public static bool operator >=(BarIndex left, BarIndex right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString();
}
