namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 17 §2e: a user-curatable enabled/disabled flag per <see cref="ExemplarEntry"/>, keyed
/// by song title + bar range (an entry has no other natural stable key) so the flag survives a re-mine
/// even though the entry's own position in <see cref="ExemplarIndex.Entries"/> can change between runs.
/// </summary>
public static class ExemplarOverrides
{
    /// <summary>The stable key a disabled/enabled flag is recorded under for one exemplar.</summary>
    public static string KeyFor(ExemplarFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        return $"{fingerprint.SongTitle}::{fingerprint.StartBarIndex}-{fingerprint.EndBarIndexExclusive}";
    }

    /// <summary>
    /// Returns a copy of <paramref name="index"/> with every entry whose key maps to <see langword="false"/>
    /// in <paramref name="disabledByKey"/> removed, so <see cref="ExemplarIndex.FindSimilar"/> run against
    /// the result never returns a disabled entry as a candidate. An entry with no key present in
    /// <paramref name="disabledByKey"/> is kept (not yet curated == enabled, the default). This is a thin
    /// pre-filter rather than a change to <see cref="ExemplarIndex.FindSimilar"/> itself, so every existing
    /// caller that never loads overrides keeps behaving exactly as before.
    /// </summary>
    public static ExemplarIndex ApplyOverrides(ExemplarIndex index, IReadOnlyDictionary<string, bool> disabledByKey)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(disabledByKey);

        if (disabledByKey.Count == 0)
        {
            return index;
        }

        var enabledEntries = index.Entries
            .Where(e => !disabledByKey.TryGetValue(KeyFor(e.Fingerprint), out var disabled) || !disabled)
            .ToList();

        return index with { Entries = enabledEntries };
    }
}
