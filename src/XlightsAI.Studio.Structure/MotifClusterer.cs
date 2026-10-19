using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 056 -- real implementation of <see cref="IMotifClusterer"/>: resamples each already-
/// approved <see cref="PhraseCandidate"/>'s own per-bar <see cref="BarFingerprint.Features"/> sequence to a
/// fixed-length, time-scale-normalized embedding, then proposes a <see cref="MotifLinkCandidate"/> between
/// every pair of phrases whose embeddings are at least <see cref="MotifClusteringSettings.SimilarityThreshold"/>
/// cosine-similar, and groups the resulting link graph into connected-component <see cref="MotifGroupCandidate"/>s.
/// Step 056's own declared recipe, verbatim: "Use self-similarity and robust time-scaling to propose
/// repeated motifs; score separately from section label."
/// </summary>
/// <remarks>
/// <b>Robust time-scaling.</b> Two phrases of different bar counts (e.g. an eight-bar chorus and a
/// nine-bar variation of the same idea) are each independently resampled, via nearest-neighbor index
/// mapping, to exactly <see cref="MotifClusteringSettings.CanonicalSampleCount"/> bars before comparison --
/// so phrase length alone never prevents two musically similar phrases from being linked.
/// <para>
/// <b>Score separately from section label.</b> This recipe never reads <see cref="SectionLabelCandidate.Kind"/>
/// or any section-level data; similarity is computed purely from <see cref="BarFingerprint.Features"/>.
/// </para>
/// <para>
/// <b>A group requires repetition.</b> A phrase with no sufficiently similar partner never appears in any
/// <see cref="MotifClusteringAnalysis.Groups"/> entry -- "distinct passage does not [share a motif ID]"
/// (this step's own required acceptance, verbatim) is satisfied by simply never emitting a singleton group.
/// </para>
/// </remarks>
public sealed class MotifClusterer : IMotifClusterer
{
    /// <summary>Burned into every <see cref="MotifClusteringAnalysis.AnalyzerVersion"/> this clusterer produces.</summary>
    public const string Version = "XlightsAI.Studio.Structure.MotifClusterer/1.0.0";

    /// <summary>This step's own single implemented recipe -- time-scaled cosine similarity with connected-component clustering. A future recipe is a different, distinguishable <see cref="MotifClusteringSettings.RecipeId"/>, not a silent change to this one's meaning.</summary>
    public const string TimeScaledCosineRecipeId = "time-scaled-cosine/1.0.0";

    private readonly IMotifClusteringStore _store;

    public MotifClusterer(IMotifClusteringStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<MotifClusteringAnalysis> ClusterAsync(MotifClusteringContext context, MotifClusteringSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        ValidateInputs(context, settings);

        ct.ThrowIfCancellationRequested();

        var path = _store.GetPath(context.SongId);

        if (await TryLoadStillValidCacheAsync(path, context, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        var phrases = context.PhraseAnalysis.Phrases.OrderBy(p => p.StartBarIndex).ToList();

        List<MotifGroupCandidate> groups;
        List<MotifLinkCandidate> links;

        if (phrases.Count < 2)
        {
            // Fewer than two phrases: no pair exists to compare -- an explicit, non-throwing empty result
            // (the same "silence... must be handled explicitly" convention Steps 052-055 already established).
            groups = [];
            links = [];
        }
        else
        {
            var barsByIndex = context.FingerprintSet.Bars.ToDictionary(b => b.BarIndex);
            ValidatePhraseBarRanges(context.SongId, phrases, barsByIndex);

            var embeddings = phrases.ToDictionary(p => p.Id, p => BuildEmbedding(p, barsByIndex, settings.CanonicalSampleCount));

            links = ComputeLinks(phrases, embeddings, settings.SimilarityThreshold);
            groups = ComputeGroups(phrases, links);
        }

        var analysis = new MotifClusteringAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: context.SongId,
            SourceHash: context.SourceHash,
            SourcePhraseSegmentationVersionId: context.PhraseAnalysis.VersionId,
            SourceFingerprintSetVersionId: context.FingerprintSet.VersionId,
            AnalyzerVersion: Version,
            Settings: settings,
            Groups: groups,
            Links: links);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private static void ValidateInputs(MotifClusteringContext context, MotifClusteringSettings settings)
    {
        if (string.IsNullOrWhiteSpace(context.SourceHash))
        {
            throw new MotifClusteringInvalidException(context.SongId, "SourceHash is empty -- missing media.");
        }

        if (context.PhraseAnalysis.SongId != context.SongId)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                $"PhraseSegmentationAnalysis.SongId ({context.PhraseAnalysis.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.PhraseAnalysis.SourceHash != context.SourceHash)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                "PhraseSegmentationAnalysis.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.FingerprintSet.SongId != context.SongId)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                $"SongFingerprintSet.SongId ({context.FingerprintSet.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.FingerprintSet.SourceHash != context.SourceHash)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                "SongFingerprintSet.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.PhraseAnalysis.SourceFingerprintSetVersionId != context.FingerprintSet.VersionId)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                "PhraseSegmentationAnalysis.SourceFingerprintSetVersionId does not match the supplied SongFingerprintSet.VersionId -- stale or mismatched analysis.");
        }

        if (settings.CanonicalSampleCount <= 0)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                $"MotifClusteringSettings.CanonicalSampleCount ({settings.CanonicalSampleCount}) must be positive.");
        }

        if (settings.SimilarityThreshold is < 0.0 or > 1.0)
        {
            throw new MotifClusteringInvalidException(context.SongId,
                $"MotifClusteringSettings.SimilarityThreshold ({settings.SimilarityThreshold}) must be in [0, 1].");
        }

        if (string.IsNullOrWhiteSpace(settings.RecipeId))
        {
            throw new MotifClusteringInvalidException(context.SongId, "MotifClusteringSettings.RecipeId must not be empty.");
        }
    }

    private async Task<MotifClusteringAnalysis?> TryLoadStillValidCacheAsync(
        string path, MotifClusteringContext context, MotifClusteringSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        MotifClusteringAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (MotifClusteringCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == context.SourceHash
            && existing.SourcePhraseSegmentationVersionId == context.PhraseAnalysis.VersionId
            && existing.SourceFingerprintSetVersionId == context.FingerprintSet.VersionId
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static void ValidatePhraseBarRanges(
        SongId songId, IReadOnlyList<PhraseCandidate> phrases, Dictionary<int, BarFingerprint> barsByIndex)
    {
        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];

            if (phrase.EndBarIndexExclusive <= phrase.StartBarIndex)
            {
                throw new MotifClusteringInvalidException(songId,
                    $"Phrase at position {i} has EndBarIndexExclusive ({phrase.EndBarIndexExclusive}) <= StartBarIndex ({phrase.StartBarIndex}) -- malformed proposal.");
            }

            for (var bar = phrase.StartBarIndex; bar < phrase.EndBarIndexExclusive; bar++)
            {
                if (!barsByIndex.ContainsKey(bar))
                {
                    throw new MotifClusteringInvalidException(songId,
                        $"Phrase at position {i} references bar {bar}, which does not exist in the source fingerprint set -- malformed/mismatched proposal.");
                }
            }
        }
    }

    /// <summary>
    /// Resamples <paramref name="phrase"/>'s own per-bar <see cref="BarFingerprint.Features"/> sequence to
    /// exactly <paramref name="canonicalSampleCount"/> samples via nearest-neighbor index mapping, then
    /// flattens the result into one comparable vector -- "robust time-scaling" (this step's own
    /// implementation guidance, verbatim).
    /// </summary>
    private static double[] BuildEmbedding(PhraseCandidate phrase, Dictionary<int, BarFingerprint> barsByIndex, int canonicalSampleCount)
    {
        var barCount = phrase.EndBarIndexExclusive - phrase.StartBarIndex;
        var featureDims = barsByIndex[phrase.StartBarIndex].Features.Count;
        var embedding = new double[canonicalSampleCount * featureDims];

        for (var sample = 0; sample < canonicalSampleCount; sample++)
        {
            var sourceOffset = sample * barCount / canonicalSampleCount;
            var sourceBarIndex = phrase.StartBarIndex + Math.Min(sourceOffset, barCount - 1);
            var features = barsByIndex[sourceBarIndex].Features;

            for (var d = 0; d < featureDims; d++)
            {
                embedding[sample * featureDims + d] = features[d];
            }
        }

        return embedding;
    }

    private static double Cosine(double[] a, double[] b)
    {
        double dot = 0.0, magA = 0.0, magB = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA <= 0.0 || magB <= 0.0)
        {
            return 0.0;
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private static List<MotifLinkCandidate> ComputeLinks(
        IReadOnlyList<PhraseCandidate> phrases, IReadOnlyDictionary<Guid, double[]> embeddings, double similarityThreshold)
    {
        var links = new List<MotifLinkCandidate>();

        for (var i = 0; i < phrases.Count; i++)
        {
            for (var j = i + 1; j < phrases.Count; j++)
            {
                var similarity = Cosine(embeddings[phrases[i].Id], embeddings[phrases[j].Id]);

                if (similarity >= similarityThreshold)
                {
                    links.Add(new MotifLinkCandidate(
                        SourcePhraseId: phrases[i].Id,
                        TargetPhraseId: phrases[j].Id,
                        Similarity: similarity,
                        Evidence:
                        [
                            $"Cosine similarity {similarity:F3} between time-scale-normalized embeddings meets or exceeds the {similarityThreshold:F3} threshold.",
                        ]));
                }
            }
        }

        return links;
    }

    /// <summary>
    /// Builds connected components over <paramref name="links"/> via union-find, discarding any component
    /// with fewer than two members ("distinct passage does not [share a motif ID]," this step's own
    /// required acceptance, verbatim -- a phrase with no accepted link is simply absent from every group).
    /// </summary>
    private static List<MotifGroupCandidate> ComputeGroups(IReadOnlyList<PhraseCandidate> phrases, IReadOnlyList<MotifLinkCandidate> links)
    {
        var indexById = new Dictionary<Guid, int>();
        for (var i = 0; i < phrases.Count; i++)
        {
            indexById[phrases[i].Id] = i;
        }

        var parent = Enumerable.Range(0, phrases.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }

            return x;
        }

        void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
            {
                parent[rootB] = rootA;
            }
        }

        foreach (var link in links)
        {
            Union(indexById[link.SourcePhraseId], indexById[link.TargetPhraseId]);
        }

        var componentsByRoot = new Dictionary<int, List<int>>();
        for (var i = 0; i < phrases.Count; i++)
        {
            var root = Find(i);
            if (!componentsByRoot.TryGetValue(root, out var members))
            {
                members = [];
                componentsByRoot[root] = members;
            }

            members.Add(i);
        }

        var minSimilarityByPair = links.ToDictionary(l => (l.SourcePhraseId, l.TargetPhraseId), l => l.Similarity);

        var groups = new List<MotifGroupCandidate>();
        foreach (var members in componentsByRoot.Values)
        {
            if (members.Count < 2)
            {
                continue;
            }

            var orderedMembers = members.OrderBy(i => phrases[i].StartBarIndex).ToList();
            var phraseIds = orderedMembers.Select(i => phrases[i].Id).ToList();
            var representative = phraseIds[0];

            var minSimilarity = double.MaxValue;
            for (var a = 0; a < orderedMembers.Count; a++)
            {
                for (var b = a + 1; b < orderedMembers.Count; b++)
                {
                    var idA = phrases[orderedMembers[a]].Id;
                    var idB = phrases[orderedMembers[b]].Id;
                    if (minSimilarityByPair.TryGetValue((idA, idB), out var sim))
                    {
                        minSimilarity = Math.Min(minSimilarity, sim);
                    }
                }
            }

            groups.Add(new MotifGroupCandidate(
                Id: Guid.NewGuid(),
                PhraseIds: phraseIds,
                RepresentativePhraseId: representative,
                Confidence: minSimilarity == double.MaxValue ? 0.0 : minSimilarity,
                Evidence:
                [
                    $"{phraseIds.Count} phrases connected via pairwise similarity (minimum pairwise similarity in this group: {(minSimilarity == double.MaxValue ? 0.0 : minSimilarity):F3}).",
                ]));
        }

        return groups.OrderBy(g => phrases.First(p => p.Id == g.RepresentativePhraseId).StartBarIndex).ToList();
    }
}
