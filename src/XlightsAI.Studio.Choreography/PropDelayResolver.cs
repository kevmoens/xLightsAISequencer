using System.Globalization;

namespace XlightsAI.Studio.Choreography;

/// <summary>
/// Architecture §4's <c>propDelays</c> ("keyed to prop mass is what makes a display read as having
/// physics rather than being synchronized switches"): parses a <c>"+60ms"</c>-shaped offset string into
/// ticks, and assigns each resolved prop to a mass tier (<c>mass</c>/<c>mid</c>/<c>accent</c>) by its
/// own pixel weight relative to the other props a Look's layer actually targets -- the heaviest third of
/// targeted props is <c>mass</c> (moves first/no delay), the lightest third is <c>accent</c> (moves
/// last), matching the physical intuition "big things lead, small things trail" the architecture text
/// describes. This is a disclosed, simple tiering heuristic, not a verified physics model.
/// </summary>
public static class PropDelayResolver
{
    private const long TicksPerSecond = 10_000_000L;
    private const long TicksPerMillisecond = TicksPerSecond / 1000L;

    public const string TierMass = "mass";
    public const string TierMid = "mid";
    public const string TierAccent = "accent";

    /// <summary>Parses a signed millisecond offset string (<c>"+60ms"</c>, <c>"-10ms"</c>, <c>"0ms"</c>) into ticks. Throws for anything else rather than guessing.</summary>
    public static long ParseOffsetTicks(string offset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offset);

        var trimmed = offset.Trim();
        if (!trimmed.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"PropDelay offset '{offset}' does not end in 'ms'.", nameof(offset));
        }

        var numeric = trimmed[..^2];
        if (!int.TryParse(numeric, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ms))
        {
            throw new ArgumentException($"PropDelay offset '{offset}' is not a valid signed millisecond value.", nameof(offset));
        }

        return ms * TicksPerMillisecond;
    }

    /// <summary>
    /// Assigns each of <paramref name="props"/> (model id -> its own pixel weight) to a mass tier by
    /// terciles of pixel weight among that same set -- the heaviest third is <see cref="TierMass"/>, the
    /// middle third <see cref="TierMid"/>, the lightest third <see cref="TierAccent"/>. Ties at a tercile
    /// boundary resolve toward the heavier tier (never arbitrarily split two props of identical weight
    /// across tiers).
    /// </summary>
    public static IReadOnlyDictionary<ModelId, string> AssignTiers(IReadOnlyDictionary<ModelId, int> props)
    {
        ArgumentNullException.ThrowIfNull(props);

        if (props.Count == 0)
        {
            return new Dictionary<ModelId, string>();
        }

        var orderedDescending = props.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Value, StringComparer.Ordinal).ToList();
        var tierSize = Math.Max(1, (int)Math.Ceiling(orderedDescending.Count / 3.0));

        var result = new Dictionary<ModelId, string>(props.Count);
        for (var i = 0; i < orderedDescending.Count; i++)
        {
            var tier = i < tierSize ? TierMass : i < (2 * tierSize) ? TierMid : TierAccent;
            result[orderedDescending[i].Key] = tier;
        }

        return result;
    }

    /// <summary>
    /// Resolves each prop's own start-time offset (ticks) from <paramref name="propDelays"/> and its tier
    /// (<see cref="AssignTiers"/>). A prop in a tier <paramref name="propDelays"/> does not declare an
    /// entry for gets <c>0</c> offset -- no stagger is the safe, never-fabricated default.
    /// </summary>
    public static IReadOnlyDictionary<ModelId, long> ResolveOffsets(
        IReadOnlyDictionary<ModelId, int> props, IReadOnlyDictionary<string, string> propDelays)
    {
        var tiers = AssignTiers(props);
        var result = new Dictionary<ModelId, long>(props.Count);
        foreach (var (model, tier) in tiers)
        {
            result[model] = propDelays.TryGetValue(tier, out var offset) ? ParseOffsetTicks(offset) : 0L;
        }

        return result;
    }
}
