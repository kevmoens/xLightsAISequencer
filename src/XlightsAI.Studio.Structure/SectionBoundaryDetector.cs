using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 053 -- real implementation of <see cref="ISectionBoundaryDetector"/>: detects
/// candidate novelty/section boundaries from an already-approved <see cref="SongFingerprintSet"/> via
/// adjacent-bar cosine dissimilarity, with an adaptive threshold search bounded by a declared retry
/// budget. Step 053's own declared recipe, verbatim: "Detect novelty/changepoints and snap only when
/// evidence supports beat grid; represent uncertain and human-adjustable boundaries."
/// </summary>
/// <remarks>
/// <b>Adaptive threshold search (this step's own "error report" and "exhausted retry budget" required
/// acceptance/failure text, verbatim).</b> The search starts at
/// <see cref="SectionBoundarySettings.InitialNoveltyThreshold"/> and, if too few non-trivial candidates
/// are found, relaxes the threshold toward <see cref="SectionBoundarySettings.MinNoveltyThreshold"/> one
/// halving step at a time, up to <see cref="SectionBoundarySettings.MaxRefinementIterations"/> attempts.
/// The loop stops -- honestly, via <see cref="SectionBoundaryErrorReport"/>, never by silently
/// presenting a sparse result as a clean pass -- as soon as any of three conditions holds: (1) the
/// target <see cref="SectionBoundarySettings.MinBoundaryCount"/> is reached (converged); (2) relaxing
/// the threshold produced no additional candidates versus the previous iteration (a genuine plateau --
/// retrying further at the same effective threshold cannot improve the result); or (3) the iteration
/// budget or the threshold floor is reached first. Only case (1) sets
/// <see cref="SectionBoundaryErrorReport.Converged"/> to <see langword="true"/>.
/// <para>
/// <b>Malformed proposals never persist.</b> Every computed candidate list is structurally validated
/// (strictly increasing bar indices, every bar index actually present in the source fingerprint set)
/// before <see cref="ISectionBoundaryStore.SaveAsync"/> is ever called. A source
/// <see cref="SongFingerprintSet.Bars"/> list that is not itself ordered by
/// <see cref="BarFingerprint.BarIndex"/> (a corrupt or mismatched upstream artifact) can produce a
/// non-monotonic candidate sequence; this is caught and rejected via
/// <see cref="SectionBoundaryInvalidException"/> rather than silently committed.
/// </para>
/// </remarks>
public sealed class SectionBoundaryDetector : ISectionBoundaryDetector
{
    /// <summary>Burned into every <see cref="SectionBoundaryAnalysis.AnalyzerVersion"/> this detector produces.</summary>
    public const string Version = "XlightsAI.Studio.Structure.SectionBoundaryDetector/1.0.0";

    /// <summary>This step's own single implemented recipe -- adjacent-bar cosine dissimilarity with adaptive threshold search. A future recipe is a different, distinguishable <see cref="SectionBoundarySettings.RecipeId"/>, not a silent change to this one's meaning.</summary>
    public const string AdjacentCosineNoveltyRecipeId = "adjacent-cosine-novelty/1.0.0";

    private readonly ISectionBoundaryStore _store;

    public SectionBoundaryDetector(ISectionBoundaryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<SectionBoundaryAnalysis> DetectAsync(SectionBoundaryContext context, SectionBoundarySettings settings, CancellationToken ct)
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

        var bars = context.FingerprintSet.Bars;

        IReadOnlyList<SectionBoundaryCandidate> boundaries;
        SectionBoundaryErrorReport errorReport;

        if (bars.Count == 0)
        {
            // "silence... must be handled explicitly" (the same convention Step 052 established):
            // no bars at all cannot support any boundary proposal -- an explicit, empty, non-throwing,
            // trivially converged result.
            boundaries = [];
            errorReport = new SectionBoundaryErrorReport(
                IterationsRun: 0, Converged: true, RetryBudgetExhausted: false,
                Warnings: ["Source fingerprint set has no bars (silence); zero boundaries is the correct, trivially converged result."]);
        }
        else
        {
            (boundaries, errorReport) = RunAdaptiveSearch(bars, settings, ct);
            ValidateCandidates(context.SongId, boundaries, bars);
        }

        var analysis = new SectionBoundaryAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: context.SongId,
            SourceHash: context.SourceHash,
            SourceFingerprintSetVersionId: context.FingerprintSet.VersionId,
            AnalyzerVersion: Version,
            Settings: settings,
            Boundaries: boundaries,
            ErrorReport: errorReport);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private static void ValidateInputs(SectionBoundaryContext context, SectionBoundarySettings settings)
    {
        // "missing media... must be handled explicitly" (carried over from Step 052's own convention):
        // reject an empty source identity, or a fingerprint set that does not actually belong to the
        // declared song/source, before any computation or cache lookup.
        if (string.IsNullOrWhiteSpace(context.SourceHash))
        {
            throw new SectionBoundaryInvalidException(context.SongId, "SourceHash is empty -- missing media.");
        }

        if (context.FingerprintSet.SongId != context.SongId)
        {
            throw new SectionBoundaryInvalidException(context.SongId,
                $"SongFingerprintSet.SongId ({context.FingerprintSet.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.FingerprintSet.SourceHash != context.SourceHash)
        {
            throw new SectionBoundaryInvalidException(context.SongId,
                "SongFingerprintSet.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (settings.InitialNoveltyThreshold is <= 0.0 or > 1.0)
        {
            throw new SectionBoundaryInvalidException(context.SongId,
                $"SectionBoundarySettings.InitialNoveltyThreshold ({settings.InitialNoveltyThreshold}) must be in (0, 1].");
        }

        if (settings.MinNoveltyThreshold < 0.0 || settings.MinNoveltyThreshold > settings.InitialNoveltyThreshold)
        {
            throw new SectionBoundaryInvalidException(context.SongId,
                $"SectionBoundarySettings.MinNoveltyThreshold ({settings.MinNoveltyThreshold}) must be in [0, InitialNoveltyThreshold ({settings.InitialNoveltyThreshold})].");
        }

        if (settings.MinBoundaryCount < 0)
        {
            throw new SectionBoundaryInvalidException(context.SongId,
                $"SectionBoundarySettings.MinBoundaryCount ({settings.MinBoundaryCount}) must be non-negative.");
        }

        if (settings.MaxRefinementIterations <= 0)
        {
            throw new SectionBoundaryInvalidException(context.SongId,
                $"SectionBoundarySettings.MaxRefinementIterations ({settings.MaxRefinementIterations}) must be positive -- an exhausted retry budget must still be bounded.");
        }

        if (string.IsNullOrWhiteSpace(settings.RecipeId))
        {
            throw new SectionBoundaryInvalidException(context.SongId, "SectionBoundarySettings.RecipeId must not be empty.");
        }
    }

    private async Task<SectionBoundaryAnalysis?> TryLoadStillValidCacheAsync(
        string path, SectionBoundaryContext context, SectionBoundarySettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        SectionBoundaryAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (SectionBoundaryCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == context.SourceHash
            && existing.SourceFingerprintSetVersionId == context.FingerprintSet.VersionId
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static (IReadOnlyList<SectionBoundaryCandidate> Boundaries, SectionBoundaryErrorReport ErrorReport) RunAdaptiveSearch(
        IReadOnlyList<BarFingerprint> bars, SectionBoundarySettings settings, CancellationToken ct)
    {
        var warnings = new List<string>();
        var threshold = settings.InitialNoveltyThreshold;
        var candidates = ComputeCandidates(bars, threshold);
        var iterations = 1;
        int? previousNonTrivialCount = null;
        var converged = false;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var nonTrivialCount = candidates.Count - 1;

            if (nonTrivialCount >= settings.MinBoundaryCount)
            {
                converged = true;
                break;
            }

            if (previousNonTrivialCount == nonTrivialCount)
            {
                warnings.Add(
                    $"Threshold search plateaued at {nonTrivialCount} candidate(s) (below MinBoundaryCount {settings.MinBoundaryCount}) " +
                    $"at iteration {iterations}; stopping rather than retrying without improvement.");
                break;
            }

            if (iterations >= settings.MaxRefinementIterations)
            {
                warnings.Add(
                    $"Exhausted MaxRefinementIterations ({settings.MaxRefinementIterations}) with only {nonTrivialCount} " +
                    $"candidate(s), below MinBoundaryCount ({settings.MinBoundaryCount}).");
                break;
            }

            if (threshold <= settings.MinNoveltyThreshold)
            {
                warnings.Add(
                    $"Reached MinNoveltyThreshold floor ({settings.MinNoveltyThreshold}) with only {nonTrivialCount} " +
                    $"candidate(s), below MinBoundaryCount ({settings.MinBoundaryCount}).");
                break;
            }

            previousNonTrivialCount = nonTrivialCount;
            threshold = Math.Max(settings.MinNoveltyThreshold, threshold / 2.0);
            candidates = ComputeCandidates(bars, threshold);
            iterations++;
        }

        var errorReport = new SectionBoundaryErrorReport(
            IterationsRun: iterations,
            Converged: converged,
            RetryBudgetExhausted: !converged,
            Warnings: warnings);

        return (candidates, errorReport);
    }

    private static IReadOnlyList<SectionBoundaryCandidate> ComputeCandidates(IReadOnlyList<BarFingerprint> bars, double threshold)
    {
        var result = new List<SectionBoundaryCandidate>
        {
            new(Guid.NewGuid(), bars[0].BarIndex, bars[0].Window.StartTicks,
                NoveltyScore: 0.0, Confidence: 1.0, SnappedToBeatGrid: true, UserApproved: false),
        };

        for (var i = 1; i < bars.Count; i++)
        {
            var similarity = FingerprintSimilarity.Cosine(bars[i - 1], bars[i]);
            var novelty = Math.Clamp(1.0 - similarity, 0.0, 1.0);

            if (novelty >= threshold)
            {
                result.Add(new SectionBoundaryCandidate(
                    Guid.NewGuid(), bars[i].BarIndex, bars[i].Window.StartTicks,
                    NoveltyScore: novelty, Confidence: novelty, SnappedToBeatGrid: true, UserApproved: false));
            }
        }

        return result;
    }

    private static void ValidateCandidates(
        SongId songId, IReadOnlyList<SectionBoundaryCandidate> candidates, IReadOnlyList<BarFingerprint> bars)
    {
        if (candidates.Count == 0)
        {
            throw new SectionBoundaryInvalidException(songId,
                "Computed candidate list is empty despite a non-empty fingerprint set -- malformed proposal.");
        }

        var validBarIndices = bars.Select(b => b.BarIndex).ToHashSet();

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!validBarIndices.Contains(candidates[i].BarIndex))
            {
                throw new SectionBoundaryInvalidException(songId,
                    $"Candidate at position {i} references BarIndex {candidates[i].BarIndex}, which does not exist in the source fingerprint set -- malformed proposal.");
            }

            if (i > 0 && candidates[i].BarIndex <= candidates[i - 1].BarIndex)
            {
                throw new SectionBoundaryInvalidException(songId,
                    $"Candidate boundaries are not strictly increasing by BarIndex ({candidates[i - 1].BarIndex} -> {candidates[i].BarIndex}) -- malformed proposal.");
            }
        }
    }
}
