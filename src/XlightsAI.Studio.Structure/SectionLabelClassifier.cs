using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 054 -- real implementation of <see cref="ISectionLabelClassifier"/>: classifies every
/// already-approved <see cref="SectionBoundaryCandidate"/> segment from an already-approved
/// <see cref="SongFingerprintSet"/> using two declared, inspectable evidence signals -- segment
/// *repetition* (clustering averaged per-segment fingerprints by Step 052's own
/// <see cref="FingerprintSimilarity.Cosine"/>) and segment *position* (first/last/between-repeats) --
/// then merges in any persisted <see cref="ISectionLabelOverrideStore"/> entries. Step 054's own declared
/// recipe, verbatim: "Classify section labels with confidence and evidence; Unknown is valid; never force
/// classical song structure."
/// </summary>
/// <remarks>
/// <b>No iterative retry loop (an explicit, disclosed architecture decision).</b> Step 054's own
/// failure/edge-case text is the same boilerplate clause Steps 053/055-057 all share verbatim ("Malformed
/// proposal, no-improvement and exhausted retry budget must not auto-commit"). Unlike Step 053's own
/// adaptive novelty-threshold search, this step's own recipe is a single deterministic evidence
/// computation per segment -- there is no meaningful "retry budget" to exhaust, because there is no search
/// loop to retry. The shared clause's intent ("never auto-commit a result the evidence does not actually
/// support") is instead satisfied structurally: a segment whose computed <see cref="SectionLabelCandidate.Confidence"/>
/// does not clear <see cref="SectionLabelSettings.MinConfidenceToLabel"/> is classified
/// <see cref="SectionKind.Unknown"/> -- this step's own explicit, named substitute for "no-improvement...
/// must not auto-commit" (Step 054's own required acceptance, verbatim: "Unknown is valid; never force
/// classical song structure"). "Malformed proposal... must not auto-commit" itself is satisfied literally:
/// a structurally invalid upstream boundary list, or an override targeting a boundary id that does not
/// exist, is rejected via <see cref="SectionLabelInvalidException"/> before any cache or override file is
/// written.
/// <para>
/// <b>Overrides are applied at read time, not baked into the cached automatic analysis.</b> The automatic
/// classification cache (<see cref="ISectionLabelStore"/>) and the user override store
/// (<see cref="ISectionLabelOverrideStore"/>) are independently versioned/invalidated (Section 4). Every
/// <see cref="ClassifyAsync"/> call re-reads the override store and re-merges it onto whatever automatic
/// result is current -- an upstream re-analysis that invalidates and rebuilds the automatic cache, or an
/// application restart that creates brand-new store instances over the same directory, both still surface
/// a previously recorded override unchanged. This is Step 054's own required acceptance, verbatim:
/// "overrides survive restart."
/// </para>
/// </remarks>
public sealed class SectionLabelClassifier : ISectionLabelClassifier
{
    /// <summary>Burned into every <see cref="SectionLabelAnalysis.AnalyzerVersion"/> this classifier produces.</summary>
    public const string Version = "XlightsAI.Studio.Structure.SectionLabelClassifier/1.0.0";

    /// <summary>This step's own single implemented recipe -- fingerprint-repetition clustering plus segment-position heuristics. A future recipe is a different, distinguishable <see cref="SectionLabelSettings.RecipeId"/>, not a silent change to this one's meaning.</summary>
    public const string RepetitionAndPositionRecipeId = "repetition-and-position/1.0.0";

    private const double PositionHeuristicConfidence = 0.6;
    private const double DefaultInternalSegmentConfidence = 0.5;

    private readonly ISectionLabelStore _store;
    private readonly ISectionLabelOverrideStore _overrideStore;

    public SectionLabelClassifier(ISectionLabelStore store, ISectionLabelOverrideStore overrideStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _overrideStore = overrideStore ?? throw new ArgumentNullException(nameof(overrideStore));
    }

    public async Task<SectionLabelAnalysis> ClassifyAsync(SectionLabelContext context, SectionLabelSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var raw = await BuildOrLoadRawAsync(context, settings, ct).ConfigureAwait(false);
        var overrides = await _overrideStore.LoadAsync(context.SongId, ct).ConfigureAwait(false);
        return ApplyOverrides(raw, overrides);
    }

    public async Task<SectionLabelAnalysis> SetOverrideAsync(
        SectionLabelContext context, SectionLabelSettings settings, Guid boundaryId, SectionKind kind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var raw = await BuildOrLoadRawAsync(context, settings, ct).ConfigureAwait(false);

        if (raw.Labels.All(l => l.BoundaryId != boundaryId))
        {
            throw new SectionLabelInvalidException(context.SongId,
                $"Cannot set override: BoundaryId '{boundaryId}' does not reference any segment in the current classification -- malformed override target, not persisted.");
        }

        var existing = await _overrideStore.LoadAsync(context.SongId, ct).ConfigureAwait(false);
        var updated = new Dictionary<Guid, SectionKind>(existing) { [boundaryId] = kind };

        await _overrideStore.SaveAsync(context.SongId, updated, ct).ConfigureAwait(false);

        return ApplyOverrides(raw, updated);
    }

    private static SectionLabelAnalysis ApplyOverrides(SectionLabelAnalysis raw, IReadOnlyDictionary<Guid, SectionKind> overrides)
    {
        if (overrides.Count == 0)
        {
            return raw;
        }

        var merged = raw.Labels
            .Select(label => overrides.TryGetValue(label.BoundaryId, out var kind)
                ? label with
                {
                    Kind = kind,
                    Confidence = 1.0,
                    Evidence = ["User override applied; automatic classification superseded."],
                    UserCorrected = true,
                }
                : label)
            .ToList();

        return raw with { Labels = merged };
    }

    private async Task<SectionLabelAnalysis> BuildOrLoadRawAsync(SectionLabelContext context, SectionLabelSettings settings, CancellationToken ct)
    {
        ValidateInputs(context, settings);

        ct.ThrowIfCancellationRequested();

        var path = _store.GetPath(context.SongId);

        if (await TryLoadStillValidCacheAsync(path, context, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        var boundaries = context.BoundaryAnalysis.Boundaries;
        var bars = context.FingerprintSet.Bars;

        IReadOnlyList<SectionLabelCandidate> labels;

        if (boundaries.Count == 0)
        {
            // "silence... must be handled explicitly" (the same convention Steps 052/053 established):
            // no boundaries at all cannot support any segment classification -- an explicit, empty,
            // non-throwing result.
            labels = [];
        }
        else
        {
            ValidateBoundaries(context.SongId, boundaries, bars);
            labels = ClassifySegments(boundaries, bars, settings);
        }

        var analysis = new SectionLabelAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: context.SongId,
            SourceHash: context.SourceHash,
            SourceSectionBoundaryVersionId: context.BoundaryAnalysis.VersionId,
            SourceFingerprintSetVersionId: context.FingerprintSet.VersionId,
            AnalyzerVersion: Version,
            Settings: settings,
            Labels: labels);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private static void ValidateInputs(SectionLabelContext context, SectionLabelSettings settings)
    {
        if (string.IsNullOrWhiteSpace(context.SourceHash))
        {
            throw new SectionLabelInvalidException(context.SongId, "SourceHash is empty -- missing media.");
        }

        if (context.BoundaryAnalysis.SongId != context.SongId)
        {
            throw new SectionLabelInvalidException(context.SongId,
                $"SectionBoundaryAnalysis.SongId ({context.BoundaryAnalysis.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.BoundaryAnalysis.SourceHash != context.SourceHash)
        {
            throw new SectionLabelInvalidException(context.SongId,
                "SectionBoundaryAnalysis.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.FingerprintSet.SongId != context.SongId)
        {
            throw new SectionLabelInvalidException(context.SongId,
                $"SongFingerprintSet.SongId ({context.FingerprintSet.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.FingerprintSet.SourceHash != context.SourceHash)
        {
            throw new SectionLabelInvalidException(context.SongId,
                "SongFingerprintSet.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.BoundaryAnalysis.SourceFingerprintSetVersionId != context.FingerprintSet.VersionId)
        {
            throw new SectionLabelInvalidException(context.SongId,
                "SectionBoundaryAnalysis.SourceFingerprintSetVersionId does not match the supplied SongFingerprintSet.VersionId -- stale or mismatched analysis.");
        }

        if (settings.RepetitionSimilarityThreshold is <= 0.0 or > 1.0)
        {
            throw new SectionLabelInvalidException(context.SongId,
                $"SectionLabelSettings.RepetitionSimilarityThreshold ({settings.RepetitionSimilarityThreshold}) must be in (0, 1].");
        }

        if (settings.MinConfidenceToLabel is < 0.0 or > 1.0)
        {
            throw new SectionLabelInvalidException(context.SongId,
                $"SectionLabelSettings.MinConfidenceToLabel ({settings.MinConfidenceToLabel}) must be in [0, 1].");
        }

        if (string.IsNullOrWhiteSpace(settings.RecipeId))
        {
            throw new SectionLabelInvalidException(context.SongId, "SectionLabelSettings.RecipeId must not be empty.");
        }
    }

    private async Task<SectionLabelAnalysis?> TryLoadStillValidCacheAsync(
        string path, SectionLabelContext context, SectionLabelSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        SectionLabelAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (SectionLabelCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == context.SourceHash
            && existing.SourceSectionBoundaryVersionId == context.BoundaryAnalysis.VersionId
            && existing.SourceFingerprintSetVersionId == context.FingerprintSet.VersionId
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static void ValidateBoundaries(
        SongId songId, IReadOnlyList<SectionBoundaryCandidate> boundaries, IReadOnlyList<BarFingerprint> bars)
    {
        if (bars.Count == 0)
        {
            throw new SectionLabelInvalidException(songId,
                "SectionBoundaryAnalysis has boundaries but the source SongFingerprintSet has no bars -- malformed/mismatched proposal.");
        }

        var validBarIndices = bars.Select(b => b.BarIndex).ToHashSet();

        for (var i = 0; i < boundaries.Count; i++)
        {
            if (!validBarIndices.Contains(boundaries[i].BarIndex))
            {
                throw new SectionLabelInvalidException(songId,
                    $"Boundary at position {i} references BarIndex {boundaries[i].BarIndex}, which does not exist in the source fingerprint set -- malformed proposal.");
            }

            if (i > 0 && boundaries[i].BarIndex <= boundaries[i - 1].BarIndex)
            {
                throw new SectionLabelInvalidException(songId,
                    $"Boundaries are not strictly increasing by BarIndex ({boundaries[i - 1].BarIndex} -> {boundaries[i].BarIndex}) -- malformed proposal.");
            }
        }
    }

    private static List<SectionLabelCandidate> ClassifySegments(
        IReadOnlyList<SectionBoundaryCandidate> boundaries, IReadOnlyList<BarFingerprint> bars, SectionLabelSettings settings)
    {
        var segmentCount = boundaries.Count;
        var averages = new BarFingerprint[segmentCount];
        var allSilent = new bool[segmentCount];

        for (var i = 0; i < segmentCount; i++)
        {
            var startBar = boundaries[i].BarIndex;
            var endBarExclusive = i + 1 < segmentCount ? boundaries[i + 1].BarIndex : bars[^1].BarIndex + 1;
            var segmentBars = bars.Where(b => b.BarIndex >= startBar && b.BarIndex < endBarExclusive).ToList();

            allSilent[i] = segmentBars.Count > 0 && segmentBars.All(b => b.IsSilent);
            averages[i] = AverageFingerprint(segmentBars);
        }

        // Union-find clustering: two segments join the same cluster when their averaged fingerprints'
        // cosine similarity meets RepetitionSimilarityThreshold -- "robust... tolerance for variation"
        // (Section 3) is deliberately out of this step's own narrower scope (Step 056's own later scope:
        // "Cluster exact and similar motifs with tolerance for variation"); this step only needs enough
        // repetition evidence to inform a *label*, not to build a motif graph.
        var parent = Enumerable.Range(0, segmentCount).ToArray();
        var similarities = new Dictionary<(int, int), double>();

        for (var i = 0; i < segmentCount; i++)
        {
            for (var j = i + 1; j < segmentCount; j++)
            {
                if (allSilent[i] || allSilent[j])
                {
                    continue;
                }

                var similarity = FingerprintSimilarity.Cosine(averages[i], averages[j]);
                if (similarity >= settings.RepetitionSimilarityThreshold)
                {
                    similarities[(i, j)] = similarity;
                    Union(parent, i, j);
                }
            }
        }

        var clusterMembers = new Dictionary<int, List<int>>();
        for (var i = 0; i < segmentCount; i++)
        {
            var root = Find(parent, i);
            if (!clusterMembers.TryGetValue(root, out var members))
            {
                clusterMembers[root] = members = [];
            }

            members.Add(i);
        }

        var result = new List<SectionLabelCandidate>(segmentCount);

        for (var i = 0; i < segmentCount; i++)
        {
            var (kind, confidence, evidence) = ClassifySegment(
                i, segmentCount, allSilent[i], parent, clusterMembers, similarities, settings);

            var startBar = boundaries[i].BarIndex;
            var endBarExclusive = i + 1 < segmentCount ? boundaries[i + 1].BarIndex : bars[^1].BarIndex + 1;

            result.Add(new SectionLabelCandidate(
                Id: Guid.NewGuid(),
                BoundaryId: boundaries[i].Id,
                StartBarIndex: startBar,
                EndBarIndexExclusive: endBarExclusive,
                Kind: kind,
                Confidence: confidence,
                Evidence: evidence,
                UserCorrected: false));
        }

        return result;
    }

    private static (SectionKind Kind, double Confidence, IReadOnlyList<string> Evidence) ClassifySegment(
        int index,
        int segmentCount,
        bool isSilent,
        int[] parent,
        Dictionary<int, List<int>> clusterMembers,
        Dictionary<(int, int), double> similarities,
        SectionLabelSettings settings)
    {
        if (isSilent)
        {
            return (SectionKind.Unknown, 0.0,
                ["Segment has no measurable energy or melodic content; insufficient evidence to classify."]);
        }

        if (segmentCount == 1)
        {
            // "never force classical song structure" (verbatim): a single-segment song is neither
            // meaningfully an intro nor an outro -- there is nothing else to be the middle of.
            return (SectionKind.Unknown, 0.0,
                ["Only one segment exists in this song; insufficient evidence to distinguish any song structure."]);
        }

        var root = Find(parent, index);
        var clusterPeers = clusterMembers[root].Where(m => m != index).ToList();

        if (clusterPeers.Count > 0)
        {
            var avgSimilarity = clusterPeers
                .Select(peer => similarities.TryGetValue((Math.Min(index, peer), Math.Max(index, peer)), out var s) ? s : 0.0)
                .Average();

            return Finalize(SectionKind.Chorus, Math.Clamp(avgSimilarity, 0.0, 1.0), settings,
                [$"Recurs with {clusterPeers.Count} other segment(s) at average fingerprint similarity {avgSimilarity:F2}, at or above RepetitionSimilarityThreshold ({settings.RepetitionSimilarityThreshold:F2})."]);
        }

        if (index == 0)
        {
            return Finalize(SectionKind.Intro, PositionHeuristicConfidence, settings,
                ["First segment in the song; no repetition detected elsewhere."]);
        }

        if (index == segmentCount - 1)
        {
            return Finalize(SectionKind.Outro, PositionHeuristicConfidence, settings,
                ["Last segment in the song; no repetition detected elsewhere."]);
        }

        var previousIsChorus = IsChorusByRepetition(index - 1, parent, clusterMembers);
        var nextIsChorus = IsChorusByRepetition(index + 1, parent, clusterMembers);

        if (previousIsChorus && nextIsChorus)
        {
            return Finalize(SectionKind.Bridge, DefaultInternalSegmentConfidence, settings,
                ["Unique, non-repeating segment occurring directly between two repeated (chorus-like) segments."]);
        }

        return Finalize(SectionKind.Verse, DefaultInternalSegmentConfidence, settings,
            ["Internal, non-repeating segment with no stronger positional or repetition evidence."]);
    }

    private static bool IsChorusByRepetition(int index, int[] parent, Dictionary<int, List<int>> clusterMembers) =>
        clusterMembers[Find(parent, index)].Count > 1;

    private static (SectionKind Kind, double Confidence, IReadOnlyList<string> Evidence) Finalize(
        SectionKind kind, double confidence, SectionLabelSettings settings, List<string> evidence)
    {
        if (confidence < settings.MinConfidenceToLabel)
        {
            evidence.Add(
                $"Computed confidence ({confidence:F2}) is below MinConfidenceToLabel ({settings.MinConfidenceToLabel:F2}); labeled Unknown rather than forcing a classification.");
            return (SectionKind.Unknown, confidence, evidence);
        }

        return (kind, confidence, evidence);
    }

    private static BarFingerprint AverageFingerprint(IReadOnlyList<BarFingerprint> segmentBars)
    {
        if (segmentBars.Count == 0)
        {
            return new BarFingerprint(0, default, [], 0.0, true);
        }

        var dimensions = segmentBars[0].Features.Count;
        var sum = new double[dimensions];

        foreach (var bar in segmentBars)
        {
            for (var d = 0; d < dimensions; d++)
            {
                sum[d] += bar.Features[d];
            }
        }

        for (var d = 0; d < dimensions; d++)
        {
            sum[d] /= segmentBars.Count;
        }

        return new BarFingerprint(0, default, sum, 1.0, false);
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i)
        {
            parent[i] = parent[parent[i]];
            i = parent[i];
        }

        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        var rootA = Find(parent, a);
        var rootB = Find(parent, b);
        if (rootA != rootB)
        {
            parent[rootB] = rootA;
        }
    }
}
