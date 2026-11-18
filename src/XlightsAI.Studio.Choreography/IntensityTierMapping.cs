namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Build-plan Phase 28a §5 item 3: named, tunable Duty constants per <see cref="IntensityTier"/> --
/// "Off -&gt; Duty 0, Dim -&gt; Duty 0.15, Bright -&gt; Duty 0.6, Full -&gt; Duty 0.95," disclosed, explicit
/// constants, never invented per-call. <see cref="SectionTimelineCompiler"/> is the only consumer.
/// </summary>
public static class IntensityTierMapping
{
    public const double OffDuty = 0.0;
    public const double DimDuty = 0.15;
    public const double BrightDuty = 0.6;
    public const double FullDuty = 0.95;

    /// <exception cref="ArgumentException"><paramref name="tier"/> is not one of <see cref="IntensityTier"/>'s own four declared values.</exception>
    public static double ResolveDuty(string tier) => tier switch
    {
        IntensityTier.Off => OffDuty,
        IntensityTier.Dim => DimDuty,
        IntensityTier.Bright => BrightDuty,
        IntensityTier.Full => FullDuty,
        _ => throw new ArgumentException($"'{tier}' is not one of IntensityTier's own declared tiers (Off/Dim/Bright/Full).", nameof(tier)),
    };

    /// <summary>
    /// Build-plan Phase 28a §3's contrast rule needs a real, ordered "is this tier a step UP from that
    /// one" comparison -- the same ordinal position <see cref="ResolveDuty"/>'s own Duty values already
    /// imply (Off &lt; Dim &lt; Bright &lt; Full), exposed directly rather than re-deriving it from Duty
    /// comparisons at every call site.
    /// </summary>
    public static int RankOf(string tier) => tier switch
    {
        IntensityTier.Off => 0,
        IntensityTier.Dim => 1,
        IntensityTier.Bright => 2,
        IntensityTier.Full => 3,
        _ => throw new ArgumentException($"'{tier}' is not one of IntensityTier's own declared tiers (Off/Dim/Bright/Full).", nameof(tier)),
    };

    /// <summary>Whether <paramref name="tier"/> is one of the two "bright" tiers (<see cref="IntensityTier.Bright"/>/<see cref="IntensityTier.Full"/>) the contrast rule cares about, vs. one of the two "dim" tiers (<see cref="IntensityTier.Off"/>/<see cref="IntensityTier.Dim"/>).</summary>
    public static bool IsBrightTier(string tier) => RankOf(tier) >= RankOf(IntensityTier.Bright);
}
