namespace XlightsAI.Studio.Choreography;

/// <summary>Build-plan Phase 4 task 6: a zero-based, song-global beat number -- see <see cref="BarIndex"/>'s own doc comment for why this is a distinct type rather than a raw <see langword="int"/>.</summary>
public readonly record struct BeatIndex(int Value)
{
    public static BeatIndex operator +(BeatIndex left, int right) => new(left.Value + right);

    public static int operator -(BeatIndex left, BeatIndex right) => left.Value - right.Value;

    public static bool operator <(BeatIndex left, BeatIndex right) => left.Value < right.Value;

    public static bool operator >(BeatIndex left, BeatIndex right) => left.Value > right.Value;

    public static bool operator <=(BeatIndex left, BeatIndex right) => left.Value <= right.Value;

    public static bool operator >=(BeatIndex left, BeatIndex right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString();
}
