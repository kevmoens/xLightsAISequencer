namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 16 §2a: the outcome of one <see cref="ExemplarIndex.FindSimilar"/> call.
/// <see cref="IsFallback"/> is <see langword="true"/> exactly when the query's own
/// <see cref="ExemplarFingerprint.SectionKind"/> has zero real entries anywhere in the index, so
/// <see cref="Entries"/> (if non-empty) were drawn from other kinds instead — the caller (e.g.
/// <c>DirectorPromptBuilder</c>) must render this honestly (e.g. "no real Solo example exists, nearest
/// available: Chorus") rather than silently labeling a cross-kind result as if it were a real match for
/// the query's kind.
/// </summary>
public sealed record ExemplarRetrievalResult(IReadOnlyList<ExemplarEntry> Entries, bool IsFallback);

/// <summary>PHASE-2b (build plan §2b.9; architecture §8b): the full set of retrievable exemplars plus cosine-similarity retrieval by query fingerprint. See <see cref="ExemplarIndexBuilder"/> for how this is built.</summary>
public sealed record ExemplarIndex(DateTimeOffset GeneratedAtUtc, IReadOnlyList<ExemplarEntry> Entries)
{
    /// <summary>
    /// Build-plan Phase 16 §2a: filters to entries whose <see cref="ExemplarFingerprint.SectionKind"/>
    /// exactly matches <paramref name="query"/>'s first — a prior version of this method ranked the
    /// *whole* index by similarity regardless of kind, which let a query for a kind with zero real
    /// entries (e.g. Solo/PreChorus against the live corpus) silently return some other kind's snippet.
    /// Only when zero same-kind entries exist anywhere in the index does this fall back to the nearest
    /// entries by similarity across every kind, and that fallback is reported via
    /// <see cref="ExemplarRetrievalResult.IsFallback"/> so the caller can disclose it rather than
    /// mislabel it.
    ///
    /// Within whichever candidate pool applies, returns the <paramref name="count"/> entries whose
    /// <see cref="ExemplarFingerprint.ToComparisonVector"/> is most cosine-similar to
    /// <paramref name="query"/>'s, descending. Cosine was chosen over Euclidean distance because the
    /// comparison vector mixes several equally-weighted, already-<c>[0,1]</c>-scaled axes — cosine
    /// similarity here reduces to comparing the vectors' *direction* (relative balance of the axes)
    /// rather than raw magnitude, which is the more natural notion of "similar" when every axis is
    /// already unit-scaled and equally weighted; either choice is defensible at this dimensionality, and
    /// this is a disclosed, swappable choice, not a derived requirement.
    ///
    /// Build-plan Phase 25 Part D.4: <paramref name="minPeakConcurrentLayerCount"/>, when given, is a hard
    /// filter applied before ranking — only entries whose own <see cref="ExemplarFingerprint.PeakConcurrentLayerCount"/>
    /// reaches at least that floor are ever considered, so the Director can deliberately request "a
    /// genuinely layered moment" instead of complexity being an accidental byproduct of whichever window
    /// the plain similarity search happens to prefer. Applied within the same-kind pool first, and (only
    /// when that pool has zero same-kind entries at all) within the cross-kind fallback pool — never
    /// fabricated when no real window meets it: a floor no real entry reaches simply yields <see cref="ExemplarRetrievalResult.Entries"/> empty.
    ///
    /// Build-plan Phase 26 bugfix: real-run finding (2026-11-11, project "Bang") -- several different
    /// <see cref="XlightsAI.Studio.Structure.SectionKind"/> queries built from an otherwise-identical
    /// comparison vector (see <see cref="ExemplarFingerprint.ToComparisonVector"/>'s own doc comment: tempo/
    /// energy/band/vocal are fixed placeholders for this query shape) and with zero real same-kind entries
    /// each independently fell back to the exact same cross-kind pool and deterministically picked the
    /// identical single top match -- inflating one real exemplar's reuse count with no actual "regeneration"
    /// ever having happened. <paramref name="excludeKeys"/> (<see cref="ExemplarOverrides.KeyFor"/> keys
    /// already drawn elsewhere in this same run, see <see cref="ExemplarReuseTracker.DrawnKeys"/>) are
    /// ranked AFTER every not-yet-drawn candidate, so a later call genuinely prefers a fresh exemplar over
    /// repeating one already quoted -- only falling back to a repeat when fewer than <paramref name="count"/>
    /// fresh candidates remain, the same "a real reuse ceiling the pool cannot satisfy" case
    /// <see cref="ShowBibleValidator.ValidateExemplarReuseOnly"/> still correctly flags.
    /// </summary>
    public ExemplarRetrievalResult FindSimilar(ExemplarFingerprint query, int count, int? minPeakConcurrentLayerCount = null, IReadOnlySet<string>? excludeKeys = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (count <= 0)
        {
            return new ExemplarRetrievalResult([], IsFallback: false);
        }

        var sameKind = Entries.Where(e => e.Fingerprint.SectionKind == query.SectionKind).ToList();
        var isFallback = sameKind.Count == 0;
        var candidates = ApplyComplexityFilter(isFallback ? Entries : sameKind, minPeakConcurrentLayerCount);

        var queryVector = query.ToComparisonVector();
        var results = candidates
            .Select(entry => (Entry: entry, Similarity: CosineSimilarity(queryVector, entry.Fingerprint.ToComparisonVector())))
            .OrderBy(x => IsAlreadyDrawn(x.Entry, excludeKeys)) // not-yet-drawn (false) before already-drawn (true).
            .ThenByDescending(x => x.Similarity)
            .Take(count)
            .Select(x => x.Entry)
            .ToList();

        return new ExemplarRetrievalResult(results, isFallback);
    }

    /// <summary>
    /// User feedback (2026-10-31): <see cref="FindSimilar"/> is purely deterministic, so regenerating the
    /// same song always retrieved the identical nearest exemplars -- a "regenerate" only ever varied
    /// color, never which effects/motion got used, because the Director was quoted the same worked
    /// examples every time. This is the opt-in alternative (see <c>CustomGenerationPrompt.StayCloseToExemplars</c>):
    /// same same-kind-first/fallback candidate pool as <see cref="FindSimilar"/>, but instead of always
    /// taking the top-<paramref name="count"/> by similarity, it draws <paramref name="count"/> entries by
    /// weighted random sampling without replacement (Efraimidis-Spirakis: each candidate's key is
    /// <c>U^(1/weight)</c> for a fresh <c>U ~ Uniform(0,1)</c>, highest keys win), weighted by similarity
    /// with a nonzero floor (<see cref="MinSimilarityWeight"/>) so every candidate -- including one with
    /// near-zero similarity -- keeps a real, non-negligible chance of being picked. A candidate still
    /// needs to be the right <see cref="ExemplarFingerprint.SectionKind"/> (or there must be none of that
    /// kind at all) to ever be drawn; this only randomizes which *otherwise-eligible* match gets quoted,
    /// it never relaxes the section-kind constraint itself.
    ///
    /// Build-plan Phase 25 Part D.4: <paramref name="minPeakConcurrentLayerCount"/> is the same hard
    /// complexity filter <see cref="FindSimilar"/> applies, evaluated before weighted sampling — see that
    /// method's own doc comment.
    ///
    /// Build-plan Phase 26 bugfix: <paramref name="excludeKeys"/> is the same already-drawn-this-run
    /// preference <see cref="FindSimilar"/> applies -- see its own doc comment for why. Applied the same
    /// way here: every not-yet-drawn candidate is ranked (by its weighted sampling key) ahead of every
    /// already-drawn one, so a repeat only happens once genuinely fresh candidates run out.
    /// </summary>
    public ExemplarRetrievalResult FindVaried(ExemplarFingerprint query, int count, Random random, int? minPeakConcurrentLayerCount = null, IReadOnlySet<string>? excludeKeys = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(random);
        if (count <= 0)
        {
            return new ExemplarRetrievalResult([], IsFallback: false);
        }

        var sameKind = Entries.Where(e => e.Fingerprint.SectionKind == query.SectionKind).ToList();
        var isFallback = sameKind.Count == 0;
        var candidates = ApplyComplexityFilter(isFallback ? Entries : sameKind, minPeakConcurrentLayerCount);
        if (candidates.Count == 0)
        {
            return new ExemplarRetrievalResult([], isFallback);
        }

        var queryVector = query.ToComparisonVector();
        var results = candidates
            .Select(entry => (
                Entry: entry,
                Weight: Math.Max(MinSimilarityWeight, CosineSimilarity(queryVector, entry.Fingerprint.ToComparisonVector()))))
            .Select(x => (x.Entry, AlreadyDrawn: IsAlreadyDrawn(x.Entry, excludeKeys), Key: Math.Pow(random.NextDouble(), 1.0 / x.Weight)))
            .OrderBy(x => x.AlreadyDrawn)
            .ThenByDescending(x => x.Key)
            .Take(count)
            .Select(x => x.Entry)
            .ToList();

        return new ExemplarRetrievalResult(results, isFallback);
    }

    /// <summary>Whether <paramref name="entry"/>'s own <see cref="ExemplarOverrides.KeyFor"/> key is in <paramref name="excludeKeys"/> -- <see langword="false"/> (never already-drawn) when no exclusion set was supplied, the pre-Phase-26 behavior every existing caller keeps.</summary>
    private static bool IsAlreadyDrawn(ExemplarEntry entry, IReadOnlySet<string>? excludeKeys) =>
        excludeKeys is not null && excludeKeys.Contains(ExemplarOverrides.KeyFor(entry.Fingerprint));

    /// <summary>
    /// The floor every candidate's similarity weight is clamped up to before <see cref="FindVaried"/>'s
    /// weighted sampling -- without it, a candidate with exactly 0 (or negative) cosine similarity would
    /// have a literal 0% chance of ever being drawn, which is the same "always the safest match" failure
    /// mode as <see cref="FindSimilar"/>, just at a different threshold. 0.05 keeps the sampling
    /// meaningfully biased toward closer matches while still giving a genuinely distant one real, if
    /// small, odds -- the explicit point of this method existing at all.
    /// </summary>
    private const double MinSimilarityWeight = 0.05;

    /// <summary>Build-plan Phase 25 Part D.4: applies the optional hard complexity floor both <see cref="FindSimilar"/> and <see cref="FindVaried"/> share. A <see langword="null"/> floor is a no-op pass-through.</summary>
    private static IReadOnlyList<ExemplarEntry> ApplyComplexityFilter(IReadOnlyList<ExemplarEntry> candidates, int? minPeakConcurrentLayerCount) =>
        minPeakConcurrentLayerCount is { } floor
            ? candidates.Where(e => e.Fingerprint.PeakConcurrentLayerCount >= floor).ToList()
            : candidates;

    private static double CosineSimilarity(double[] a, double[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
