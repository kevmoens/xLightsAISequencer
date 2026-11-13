namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 25 Part E.2: clusters exemplars into a "repeatable piece" group using the same
/// structural signature <see cref="ReuseRateMiner"/> already uses to detect in-song reuse -- same phrase
/// length (bar count), same <see cref="XlightsAI.Studio.Structure.SectionKind"/>, close energy percentile
/// (same tier bucketing as <see cref="ReuseRateMiner.EnergyTierFor"/>) -- rather than inventing a second
/// notion of "structurally similar."
/// </summary>
public static class RepeatablePieceGrouper
{
    /// <summary>
    /// The group key one <see cref="ExemplarFingerprint"/> belongs to. Two entries with the same key are
    /// considered interchangeable "the same repeatable piece" for reuse-diversity purposes.
    /// </summary>
    public static string GroupKeyFor(ExemplarFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        var phraseBars = fingerprint.EndBarIndexExclusive - fingerprint.StartBarIndex;
        var energyTier = ReuseRateMiner.EnergyTierFor(fingerprint.EnergyPercentile);
        return $"{phraseBars}bar|{fingerprint.SectionKind}|{energyTier}";
    }
}

/// <summary>One "repeatable piece" group's own reuse within a single generation run -- see <see cref="ExemplarReuseTracker"/>.</summary>
/// <param name="TotalDraws">How many exemplar draws (across every member of this group) this run recorded.</param>
/// <param name="DistinctKeysDrawn">How many distinct exemplars (by <see cref="ExemplarOverrides.KeyFor"/>) from this group were actually drawn, out of <paramref name="TotalDraws"/>.</param>
public sealed record ExemplarGroupReuseStats(int TotalDraws, int DistinctKeysDrawn);

/// <summary>
/// Build-plan Phase 25 Part E.1/E.2: the outcome of one <see cref="ExemplarReuseTracker"/>'s recording
/// across a single generation run -- how many times each individual exemplar was drawn, and how that
/// adds up at the "repeatable piece" group level. <see cref="ShowBibleValidator.ValidateExemplarReuse"/>
/// is the consumer that turns this into pass/fail validation errors.
/// </summary>
public sealed record ExemplarReuseReport(
    IReadOnlyDictionary<string, int> DrawCountByExemplarKey,
    IReadOnlyDictionary<string, ExemplarGroupReuseStats> GroupStats);

/// <summary>
/// Build-plan Phase 25 Part E.1: a per-generation-run reuse counter for <see cref="ExemplarEntry"/>
/// draws, keyed the same way <see cref="ExemplarOverrides.KeyFor"/> already keys an entry for curation.
/// <see cref="DirectorPromptBuilder"/> (Director project) owns one instance per
/// <see cref="DirectorPromptBuilder.Build"/> call -- the one place <see cref="ExemplarIndex.FindSimilar"/>/
/// <see cref="ExemplarIndex.FindVaried"/> are actually invoked for a real generation -- and records every
/// retrieval's entries into it as they are drawn, so the resulting <see cref="ExemplarReuseReport"/>
/// reflects the real, complete set of exemplars quoted into that run's own Director prompt.
/// </summary>
public sealed class ExemplarReuseTracker
{
    private readonly Dictionary<string, int> _drawCountByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _totalDrawsByGroup = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _distinctKeysByGroup = new(StringComparer.Ordinal);

    public void Record(IEnumerable<ExemplarEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            var key = ExemplarOverrides.KeyFor(entry.Fingerprint);
            _drawCountByKey[key] = _drawCountByKey.GetValueOrDefault(key) + 1;

            var group = RepeatablePieceGrouper.GroupKeyFor(entry.Fingerprint);
            _totalDrawsByGroup[group] = _totalDrawsByGroup.GetValueOrDefault(group) + 1;
            if (!_distinctKeysByGroup.TryGetValue(group, out var distinctKeys))
            {
                distinctKeys = new HashSet<string>(StringComparer.Ordinal);
                _distinctKeysByGroup[group] = distinctKeys;
            }

            distinctKeys.Add(key);
        }
    }

    /// <summary>
    /// Build-plan Phase 26 bugfix: every distinct exemplar key (<see cref="ExemplarOverrides.KeyFor"/>)
    /// drawn anywhere in this run so far -- <see cref="DirectorPromptBuilder"/> passes this to
    /// <see cref="ExemplarIndex.FindSimilar"/>/<see cref="ExemplarIndex.FindVaried"/>'s own
    /// <c>excludeKeys</c> parameter BEFORE each new per-<see cref="XlightsAI.Studio.Structure.SectionKind"/>
    /// retrieval call, so a later call genuinely prefers a fresh exemplar over one this same run already
    /// quoted, rather than two independent calls (most commonly two different Kinds that both fall back
    /// to the same cross-kind pool against an otherwise-identical query vector, see
    /// <see cref="ExemplarFingerprint.ToComparisonVector"/>'s own doc comment) deterministically
    /// re-selecting the identical top match.
    /// </summary>
    public IReadOnlySet<string> DrawnKeys => new HashSet<string>(_drawCountByKey.Keys, StringComparer.Ordinal);

    public ExemplarReuseReport BuildReport()
    {
        var groupStats = _totalDrawsByGroup.ToDictionary(
            kv => kv.Key,
            kv => new ExemplarGroupReuseStats(kv.Value, _distinctKeysByGroup[kv.Key].Count));

        return new ExemplarReuseReport(
            new Dictionary<string, int>(_drawCountByKey, StringComparer.Ordinal),
            groupStats);
    }
}
