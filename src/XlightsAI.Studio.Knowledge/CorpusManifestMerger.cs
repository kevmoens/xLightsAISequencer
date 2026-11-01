using System.Security.Cryptography;

namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// Build-plan Phase 17 §2b: deduplicates <see cref="CorpusEntry"/> instances across several
/// <see cref="CorpusManifest"/>s (typically the live working-tree manifest plus one historical,
/// git-archived manifest per mined season) by the real byte content of each entry's <c>.xsq</c> file —
/// the same unedited show carried across multiple seasons' commits (or still present, unedited, in the
/// live tree) must contribute exemplars exactly once. A SHA-256 content hash was chosen over a fuzzier
/// "same song, lightly re-edited" similarity check (see build-plan Phase 17 §3's own open question) because
/// it is unambiguous and trivially testable; a show that was genuinely re-opened and edited between
/// seasons will legitimately produce a second, distinct entry under this scheme, which is the correct
/// outcome — it really is different mined material, not a duplicate.
/// </summary>
public static class CorpusManifestMerger
{
    /// <summary>
    /// Merges <paramref name="manifests"/> in order, keeping the first entry seen for each distinct
    /// content hash and recording every later same-hash entry's <see cref="CorpusEntry.SequencePath"/>
    /// as one more <see cref="CorpusEntry.DuplicatePaths"/> entry on the one that was kept — so a
    /// manifest passed first (conventionally the live working tree) wins ties against a historical
    /// season's copy of the exact same unedited show, and no content is silently lost, only recorded as
    /// a duplicate rather than mined twice. <see cref="CorpusManifest.ExcludedPaths"/> from every input
    /// manifest are unioned as-is (a path excluded in one pass was never a candidate entry in any other).
    /// </summary>
    public static CorpusManifest MergeDeduplicatingByContentHash(IReadOnlyList<CorpusManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        if (manifests.Count == 0)
        {
            throw new ArgumentException("At least one manifest is required.", nameof(manifests));
        }

        var keptByHash = new Dictionary<string, CorpusEntry>(StringComparer.Ordinal);
        var extraDuplicatesByHash = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var excluded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in manifests)
        {
            foreach (var entry in manifest.Entries)
            {
                var hash = ComputeContentHash(entry.SequencePath);
                if (keptByHash.TryGetValue(hash, out var existing))
                {
                    var extras = extraDuplicatesByHash.GetOrAdd(hash, () => []);
                    extras.Add(entry.SequencePath);
                    extras.AddRange(entry.DuplicatePaths);
                    continue;
                }

                keptByHash[hash] = entry;
            }

            foreach (var (path, reason) in manifest.ExcludedPaths)
            {
                excluded[path] = reason;
            }
        }

        var mergedEntries = keptByHash.Select(kv =>
        {
            var (hash, entry) = (kv.Key, kv.Value);
            if (!extraDuplicatesByHash.TryGetValue(hash, out var extras))
            {
                return entry;
            }

            var combinedDuplicates = entry.DuplicatePaths.Concat(extras).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return entry with { DuplicatePaths = combinedDuplicates };
        }).ToList();

        return new CorpusManifest(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            RootDirectory: manifests[0].RootDirectory,
            Entries: mergedEntries,
            ExcludedPaths: excluded);
    }

    private static string ComputeContentHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes);
    }

    private static List<string> GetOrAdd(this Dictionary<string, List<string>> dictionary, string key, Func<List<string>> factory)
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            value = factory();
            dictionary[key] = value;
        }

        return value;
    }
}
