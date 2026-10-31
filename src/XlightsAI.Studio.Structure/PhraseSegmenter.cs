using System.Security.Cryptography;
using System.Text;
using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// PHASE-08 Step 055 -- real implementation of <see cref="IPhraseSegmenter"/>: partitions every
/// already-approved <see cref="SectionLabelCandidate"/> segment from an already-approved
/// <see cref="SongFingerprintSet"/> into one or more contiguous, bar-aligned <see cref="PhraseCandidate"/>s
/// using two declared, inspectable sources -- a plain bar-count hypothesis, refined (when a
/// <see cref="MelodyTrack"/> is available) by snapping to a nearby melodic cadence -- then merges in any
/// persisted <see cref="IPhraseBoundaryOverrideStore"/> entries. Step 055's own declared recipe, verbatim:
/// "Segment into phrases with bar-based hypothesis and melodic cadences; permit irregular length and
/// phrase overlaps only by deliberate policy."
/// </summary>
/// <remarks>
/// <b>No overlaps, ever (this recipe's own declared "deliberate policy").</b> Step 055's own implementation
/// guidance only requires phrase overlap to be a <i>deliberate</i>, disclosed policy choice, not that
/// overlaps must be supported. This recipe's own declared choice is the simplest one: phrases within one
/// section always partition that section's own bar range exactly, with no gap and no overlap. A
/// bar-hypothesis split point that a melodic cadence would snap outside the open interval between its
/// already-accepted neighbors is rejected for that one split (falls back to the unsnapped hypothesis bar)
/// rather than ever producing an overlapping or zero-length phrase.
/// <para>
/// <b>Irregular length is explicit, not accidental.</b> A section shorter than or equal to
/// <see cref="PhraseSegmentationSettings.TargetPhraseLengthBars"/> becomes exactly one phrase spanning the
/// whole section (Step 055's own "permit irregular length... only by deliberate policy," verbatim) rather
/// than an error or a forced split below the target length.
/// </para>
/// <para>
/// <b>Overrides are applied at read time, not baked into the cached automatic analysis.</b> The automatic
/// segmentation cache (<see cref="IPhraseSegmentationStore"/>) and the manual boundary override store
/// (<see cref="IPhraseBoundaryOverrideStore"/>) are independently versioned/invalidated (Section 4). Every
/// <see cref="SegmentAsync"/> call re-reads the override store and re-merges it onto whatever automatic
/// result is current -- an upstream re-analysis that invalidates and rebuilds the automatic cache, or an
/// application restart that creates brand-new store instances over the same directory, both still surface
/// a previously recorded manual correction unchanged.
/// </para>
/// </remarks>
public sealed class PhraseSegmenter : IPhraseSegmenter
{
    /// <summary>Burned into every <see cref="PhraseSegmentationAnalysis.AnalyzerVersion"/> this segmenter produces.</summary>
    public const string Version = "XlightsAI.Studio.Structure.PhraseSegmenter/1.0.0";

    /// <summary>This step's own single implemented recipe -- bar-count hypothesis refined by melodic-cadence snapping. A future recipe is a different, distinguishable <see cref="PhraseSegmentationSettings.RecipeId"/>, not a silent change to this one's meaning.</summary>
    public const string BarHypothesisWithCadenceRecipeId = "bar-hypothesis-with-cadence/1.0.0";

    private const double SectionStartConfidence = 1.0;
    private const double CadenceSnappedConfidence = 0.85;
    private const double UnsnappedHypothesisConfidence = 0.5;
    private const double SingleIrregularPhraseConfidence = 1.0;

    private readonly IPhraseSegmentationStore _store;
    private readonly IPhraseBoundaryOverrideStore _overrideStore;

    public PhraseSegmenter(IPhraseSegmentationStore store, IPhraseBoundaryOverrideStore overrideStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _overrideStore = overrideStore ?? throw new ArgumentNullException(nameof(overrideStore));
    }

    public async Task<PhraseSegmentationAnalysis> SegmentAsync(PhraseSegmentationContext context, PhraseSegmentationSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var raw = await BuildOrLoadRawAsync(context, settings, ct).ConfigureAwait(false);
        var overrides = await _overrideStore.LoadAsync(context.SongId, ct).ConfigureAwait(false);
        return ApplyOverrides(context, raw, overrides);
    }

    public async Task<PhraseSegmentationAnalysis> SetManualBoundariesAsync(
        PhraseSegmentationContext context, PhraseSegmentationSettings settings, Guid sectionLabelId, IReadOnlyList<int> boundaryBarIndices, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(boundaryBarIndices);

        var raw = await BuildOrLoadRawAsync(context, settings, ct).ConfigureAwait(false);

        var section = context.LabelAnalysis.Labels.FirstOrDefault(l => l.Id == sectionLabelId)
            ?? throw new PhraseSegmentationInvalidException(context.SongId,
                $"Cannot set manual phrase boundaries: SectionLabelId '{sectionLabelId}' does not reference any section in the current label analysis -- malformed correction target, not persisted.");

        ValidateManualBoundaries(context, section, boundaryBarIndices);

        var existing = await _overrideStore.LoadAsync(context.SongId, ct).ConfigureAwait(false);
        var updated = new Dictionary<Guid, IReadOnlyList<int>>(existing)
        {
            [sectionLabelId] = boundaryBarIndices.OrderBy(b => b).ToList(),
        };

        await _overrideStore.SaveAsync(context.SongId, updated, ct).ConfigureAwait(false);

        return ApplyOverrides(context, raw, updated);
    }

    private static void ValidateManualBoundaries(
        PhraseSegmentationContext context, SectionLabelCandidate section, IReadOnlyList<int> boundaryBarIndices)
    {
        var validBarIndices = context.FingerprintSet.Bars.Select(b => b.BarIndex).ToHashSet();
        var sorted = boundaryBarIndices.OrderBy(b => b).ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var bar = sorted[i];

            if (bar <= section.StartBarIndex || bar >= section.EndBarIndexExclusive)
            {
                throw new PhraseSegmentationInvalidException(context.SongId,
                    $"Manual phrase boundary bar {bar} falls outside section [{section.StartBarIndex}, {section.EndBarIndexExclusive}) -- malformed correction, not persisted.");
            }

            if (!validBarIndices.Contains(bar))
            {
                throw new PhraseSegmentationInvalidException(context.SongId,
                    $"Manual phrase boundary bar {bar} does not exist in the source fingerprint set -- malformed correction, not persisted.");
            }

            if (i > 0 && sorted[i] == sorted[i - 1])
            {
                throw new PhraseSegmentationInvalidException(context.SongId,
                    $"Manual phrase boundary bar {bar} is duplicated -- malformed correction, not persisted.");
            }
        }
    }

    private PhraseSegmentationAnalysis ApplyOverrides(
        PhraseSegmentationContext context, PhraseSegmentationAnalysis raw, IReadOnlyDictionary<Guid, IReadOnlyList<int>> overrides)
    {
        if (overrides.Count == 0)
        {
            return raw;
        }

        var barsByIndex = context.FingerprintSet.Bars.ToDictionary(b => b.BarIndex);
        var merged = new List<PhraseCandidate>();

        foreach (var section in context.LabelAnalysis.Labels)
        {
            if (overrides.TryGetValue(section.Id, out var boundaryBarIndices))
            {
                merged.AddRange(BuildUserChosenPhrases(section, boundaryBarIndices, barsByIndex, context.FingerprintSet.Bars));
            }
            else
            {
                merged.AddRange(raw.Phrases.Where(p => p.SectionLabelId == section.Id));
            }
        }

        return raw with { Phrases = merged.OrderBy(p => p.StartBarIndex).ToList() };
    }

    private static List<PhraseCandidate> BuildUserChosenPhrases(
        SectionLabelCandidate section, IReadOnlyList<int> boundaryBarIndices,
        Dictionary<int, BarFingerprint> barsByIndex, IReadOnlyList<BarFingerprint> allBars)
    {
        var splitPoints = new List<int> { section.StartBarIndex };
        splitPoints.AddRange(boundaryBarIndices.OrderBy(b => b));
        splitPoints.Add(section.EndBarIndexExclusive);

        var result = new List<PhraseCandidate>();
        for (var i = 0; i < splitPoints.Count - 1; i++)
        {
            var start = splitPoints[i];
            var endExclusive = splitPoints[i + 1];
            var isFirst = i == 0;

            result.Add(BuildPhrase(
                section.Id, start, endExclusive, barsByIndex, allBars,
                source: isFirst ? PhraseBoundarySource.BarHypothesis : PhraseBoundarySource.UserChosen,
                confidence: 1.0,
                evidence: isFirst
                    ? ["Phrase starts at the already-approved section boundary; internal split points for this section are user-chosen."]
                    : ["User-chosen phrase boundary; automatic segmentation superseded for this section."],
                userCorrected: true));
        }

        return result;
    }

    private async Task<PhraseSegmentationAnalysis> BuildOrLoadRawAsync(PhraseSegmentationContext context, PhraseSegmentationSettings settings, CancellationToken ct)
    {
        ValidateInputs(context, settings);

        ct.ThrowIfCancellationRequested();

        var path = _store.GetPath(context.SongId);

        if (await TryLoadStillValidCacheAsync(path, context, settings, ct).ConfigureAwait(false) is { } cached)
        {
            return cached;
        }

        var labels = context.LabelAnalysis.Labels;
        var bars = context.FingerprintSet.Bars;

        List<PhraseCandidate> phrases;

        if (labels.Count == 0)
        {
            // "silence... must be handled explicitly" (the same convention Steps 052-054 established):
            // no sections at all cannot support any phrase, an explicit, empty, non-throwing result.
            phrases = [];
        }
        else
        {
            ValidateLabels(context.SongId, labels, bars);
            phrases = SegmentAllSections(labels, bars, context.MelodyTrack, settings, ct);
            ValidatePhrases(context.SongId, phrases);
        }

        var analysis = new PhraseSegmentationAnalysis(
            VersionId: Guid.NewGuid(),
            SongId: context.SongId,
            SourceHash: context.SourceHash,
            SourceSectionLabelVersionId: context.LabelAnalysis.VersionId,
            SourceFingerprintSetVersionId: context.FingerprintSet.VersionId,
            SourceMelodyTrackId: context.MelodyTrack?.Id,
            AnalyzerVersion: Version,
            Settings: settings,
            Phrases: phrases);

        await _store.SaveAsync(analysis, ct).ConfigureAwait(false);
        return analysis;
    }

    private static void ValidateInputs(PhraseSegmentationContext context, PhraseSegmentationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(context.SourceHash))
        {
            throw new PhraseSegmentationInvalidException(context.SongId, "SourceHash is empty -- missing media.");
        }

        if (context.LabelAnalysis.SongId != context.SongId)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                $"SectionLabelAnalysis.SongId ({context.LabelAnalysis.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.LabelAnalysis.SourceHash != context.SourceHash)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                "SectionLabelAnalysis.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.FingerprintSet.SongId != context.SongId)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                $"SongFingerprintSet.SongId ({context.FingerprintSet.SongId.Value}) does not match the supplied SongId.");
        }

        if (context.FingerprintSet.SourceHash != context.SourceHash)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                "SongFingerprintSet.SourceHash does not match the supplied SourceHash -- stale or mismatched analysis.");
        }

        if (context.LabelAnalysis.SourceFingerprintSetVersionId != context.FingerprintSet.VersionId)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                "SectionLabelAnalysis.SourceFingerprintSetVersionId does not match the supplied SongFingerprintSet.VersionId -- stale or mismatched analysis.");
        }

        if (context.MelodyTrack is not null)
        {
            // Deliberately compared against MelodySourceAssetHash, not SourceHash: a real MelodyTrack is
            // routinely derived from an isolated lead stem (see PhraseSegmentationContext's own doc
            // comment and StructureAnalysisRunner), whose hash is legitimately never the full song's own
            // hash. MelodySourceAssetHash is this context's own explicitly threaded-through answer to
            // "what should MelodyTrack.SourceAssetHash actually equal" -- required whenever a MelodyTrack
            // is supplied at all.
            if (string.IsNullOrWhiteSpace(context.MelodySourceAssetHash))
            {
                throw new PhraseSegmentationInvalidException(context.SongId,
                    "PhraseSegmentationContext.MelodySourceAssetHash is empty but a MelodyTrack was supplied -- missing provenance.");
            }

            if (context.MelodyTrack.SourceAssetHash != context.MelodySourceAssetHash)
            {
                throw new PhraseSegmentationInvalidException(context.SongId,
                    "MelodyTrack.SourceAssetHash does not match PhraseSegmentationContext.MelodySourceAssetHash -- stale or mismatched analysis.");
            }
        }

        if (settings.TargetPhraseLengthBars <= 0)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                $"PhraseSegmentationSettings.TargetPhraseLengthBars ({settings.TargetPhraseLengthBars}) must be positive.");
        }

        if (settings.CadenceSearchRadiusBars < 0)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                $"PhraseSegmentationSettings.CadenceSearchRadiusBars ({settings.CadenceSearchRadiusBars}) must be non-negative.");
        }

        if (settings.MinCadenceGapTicks < 0)
        {
            throw new PhraseSegmentationInvalidException(context.SongId,
                $"PhraseSegmentationSettings.MinCadenceGapTicks ({settings.MinCadenceGapTicks}) must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(settings.RecipeId))
        {
            throw new PhraseSegmentationInvalidException(context.SongId, "PhraseSegmentationSettings.RecipeId must not be empty.");
        }
    }

    private async Task<PhraseSegmentationAnalysis?> TryLoadStillValidCacheAsync(
        string path, PhraseSegmentationContext context, PhraseSegmentationSettings settings, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        PhraseSegmentationAnalysis existing;
        try
        {
            existing = await _store.LoadAsync(path, ct).ConfigureAwait(false);
        }
        catch (PhraseSegmentationCorruptException)
        {
            return null;
        }

        var stillValid = existing.SourceHash == context.SourceHash
            && existing.SourceSectionLabelVersionId == context.LabelAnalysis.VersionId
            && existing.SourceFingerprintSetVersionId == context.FingerprintSet.VersionId
            && existing.SourceMelodyTrackId == context.MelodyTrack?.Id
            && existing.AnalyzerVersion == Version
            && existing.Settings == settings;

        return stillValid ? existing : null;
    }

    private static void ValidateLabels(SongId songId, IReadOnlyList<SectionLabelCandidate> labels, IReadOnlyList<BarFingerprint> bars)
    {
        if (bars.Count == 0)
        {
            throw new PhraseSegmentationInvalidException(songId,
                "SectionLabelAnalysis has labels but the source SongFingerprintSet has no bars -- malformed/mismatched proposal.");
        }

        var validBarIndices = bars.Select(b => b.BarIndex).ToHashSet();

        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];

            if (label.EndBarIndexExclusive <= label.StartBarIndex)
            {
                throw new PhraseSegmentationInvalidException(songId,
                    $"Section at position {i} has EndBarIndexExclusive ({label.EndBarIndexExclusive}) <= StartBarIndex ({label.StartBarIndex}) -- malformed proposal.");
            }

            if (!validBarIndices.Contains(label.StartBarIndex) || !validBarIndices.Contains(label.EndBarIndexExclusive - 1))
            {
                throw new PhraseSegmentationInvalidException(songId,
                    $"Section at position {i} references a bar range [{label.StartBarIndex}, {label.EndBarIndexExclusive}) outside the source fingerprint set -- malformed proposal.");
            }

            if (i > 0 && label.StartBarIndex < labels[i - 1].StartBarIndex)
            {
                throw new PhraseSegmentationInvalidException(songId,
                    $"Sections are not ordered by StartBarIndex ({labels[i - 1].StartBarIndex} -> {label.StartBarIndex}) -- malformed proposal.");
            }
        }
    }

    private static List<PhraseCandidate> SegmentAllSections(
        IReadOnlyList<SectionLabelCandidate> labels, IReadOnlyList<BarFingerprint> bars,
        MelodyTrack? melodyTrack, PhraseSegmentationSettings settings, CancellationToken ct)
    {
        var barsByIndex = bars.ToDictionary(b => b.BarIndex);
        var cadenceBarIndices = melodyTrack is null ? [] : FindCadenceBarIndices(melodyTrack, bars, settings.MinCadenceGapTicks);

        var result = new List<PhraseCandidate>();
        foreach (var section in labels)
        {
            ct.ThrowIfCancellationRequested();
            result.AddRange(SegmentSection(section, barsByIndex, bars, cadenceBarIndices, settings));
        }

        return result;
    }

    private static List<PhraseCandidate> SegmentSection(
        SectionLabelCandidate section, Dictionary<int, BarFingerprint> barsByIndex, IReadOnlyList<BarFingerprint> allBars,
        IReadOnlyList<int> cadenceBarIndices, PhraseSegmentationSettings settings)
    {
        var sectionLengthBars = section.EndBarIndexExclusive - section.StartBarIndex;

        if (sectionLengthBars <= settings.TargetPhraseLengthBars)
        {
            return
            [
                BuildPhrase(section.Id, section.StartBarIndex, section.EndBarIndexExclusive, barsByIndex, allBars,
                    PhraseBoundarySource.BarHypothesis, SingleIrregularPhraseConfidence,
                    ["Section is shorter than or equal to TargetPhraseLengthBars; entire section is a single irregular-length phrase (declared policy)."],
                    userCorrected: false),
            ];
        }

        var hypothesisPoints = new List<int>();
        for (var bar = section.StartBarIndex + settings.TargetPhraseLengthBars; bar < section.EndBarIndexExclusive; bar += settings.TargetPhraseLengthBars)
        {
            hypothesisPoints.Add(bar);
        }

        var acceptedSplits = new List<(int Bar, PhraseBoundarySource Source, double Confidence, string Evidence)>
        {
            (section.StartBarIndex, PhraseBoundarySource.BarHypothesis, SectionStartConfidence, "Phrase starts at the already-approved section boundary."),
        };

        for (var i = 0; i < hypothesisPoints.Count; i++)
        {
            var hypothesisBar = hypothesisPoints[i];
            var previousAccepted = acceptedSplits[^1].Bar;
            var nextHypothesisOrEnd = i + 1 < hypothesisPoints.Count ? hypothesisPoints[i + 1] : section.EndBarIndexExclusive;

            var snapped = FindNearestCadenceWithinRadius(
                hypothesisBar, settings.CadenceSearchRadiusBars, cadenceBarIndices, previousAccepted, nextHypothesisOrEnd);

            if (snapped is { } cadenceBar)
            {
                var distance = Math.Abs(cadenceBar - hypothesisBar);
                acceptedSplits.Add((cadenceBar, PhraseBoundarySource.MelodicCadence, CadenceSnappedConfidence,
                    $"Phrase boundary snapped to a melodic cadence at bar {cadenceBar}, {distance} bar(s) from the bar-based hypothesis (bar {hypothesisBar})."));
            }
            else
            {
                acceptedSplits.Add((hypothesisBar, PhraseBoundarySource.BarHypothesis, UnsnappedHypothesisConfidence,
                    $"No melodic cadence found within {settings.CadenceSearchRadiusBars} bar(s) of the bar-based hypothesis target length of {settings.TargetPhraseLengthBars} bars; using the unsnapped hypothesis boundary."));
            }
        }

        var result = new List<PhraseCandidate>();
        for (var i = 0; i < acceptedSplits.Count; i++)
        {
            var (start, source, confidence, evidence) = acceptedSplits[i];
            var endExclusive = i + 1 < acceptedSplits.Count ? acceptedSplits[i + 1].Bar : section.EndBarIndexExclusive;

            result.Add(BuildPhrase(section.Id, start, endExclusive, barsByIndex, allBars, source, confidence, [evidence], userCorrected: false));
        }

        return result;
    }

    /// <summary>
    /// Finds every bar index at which a melody rest at least <paramref name="minCadenceGapTicks"/> long
    /// begins -- either between two consecutive notes, or after the final note ends. Deterministic and
    /// independent of search radius/hypothesis position; <see cref="FindNearestCadenceWithinRadius"/> then
    /// picks which (if any) of these actually applies to one specific hypothesis boundary.
    /// </summary>
    private static List<int> FindCadenceBarIndices(MelodyTrack melodyTrack, IReadOnlyList<BarFingerprint> bars, long minCadenceGapTicks)
    {
        if (bars.Count == 0)
        {
            return [];
        }

        var orderedNotes = melodyTrack.Notes.OrderBy(n => n.StartTicks).ToList();
        var gapStarts = new List<long>();

        for (var i = 0; i < orderedNotes.Count - 1; i++)
        {
            var gap = orderedNotes[i + 1].StartTicks - orderedNotes[i].EndTicks;
            if (gap >= minCadenceGapTicks)
            {
                gapStarts.Add(orderedNotes[i].EndTicks);
            }
        }

        if (orderedNotes.Count > 0)
        {
            var lastEnd = orderedNotes[^1].EndTicks;
            var songEnd = bars[^1].Window.EndTicks;
            if (songEnd - lastEnd >= minCadenceGapTicks)
            {
                gapStarts.Add(lastEnd);
            }
        }

        var result = new List<int>();
        foreach (var tick in gapStarts)
        {
            var bar = bars.FirstOrDefault(b => tick >= b.Window.StartTicks && tick < b.Window.EndTicks);
            if (bar is not null)
            {
                result.Add(bar.BarIndex);
            }
        }

        return result.Distinct().OrderBy(b => b).ToList();
    }

    private static int? FindNearestCadenceWithinRadius(
        int hypothesisBar, int radius, IReadOnlyList<int> cadenceBarIndices, int exclusiveLowerBound, int exclusiveUpperBound)
    {
        return cadenceBarIndices
            .Where(bar => bar > exclusiveLowerBound && bar < exclusiveUpperBound)
            .Where(bar => Math.Abs(bar - hypothesisBar) <= radius)
            .OrderBy(bar => Math.Abs(bar - hypothesisBar))
            .ThenBy(bar => bar)
            .Cast<int?>()
            .FirstOrDefault();
    }

    private static void ValidatePhrases(SongId songId, IReadOnlyList<PhraseCandidate> phrases)
    {
        for (var i = 1; i < phrases.Count; i++)
        {
            if (phrases[i].StartBarIndex < phrases[i - 1].EndBarIndexExclusive)
            {
                throw new PhraseSegmentationInvalidException(songId,
                    $"Computed phrases overlap (phrase ending at bar {phrases[i - 1].EndBarIndexExclusive} followed by one starting at bar {phrases[i].StartBarIndex}) -- malformed proposal.");
            }
        }
    }

    private static PhraseCandidate BuildPhrase(
        Guid sectionLabelId, int startBarIndex, int endBarIndexExclusive,
        Dictionary<int, BarFingerprint> barsByIndex, IReadOnlyList<BarFingerprint> allBars,
        PhraseBoundarySource source, double confidence, IReadOnlyList<string> evidence, bool userCorrected)
    {
        var startTicks = barsByIndex[startBarIndex].Window.StartTicks;
        var endTicks = barsByIndex.TryGetValue(endBarIndexExclusive, out var endBar)
            ? endBar.Window.StartTicks
            : allBars[^1].Window.EndTicks;

        var phraseBars = allBars.Where(b => b.BarIndex >= startBarIndex && b.BarIndex < endBarIndexExclusive).ToList();

        return new PhraseCandidate(
            Id: Guid.NewGuid(),
            SectionLabelId: sectionLabelId,
            StartBarIndex: startBarIndex,
            EndBarIndexExclusive: endBarIndexExclusive,
            StartTicks: startTicks,
            EndTicks: endTicks,
            Bars: endBarIndexExclusive - startBarIndex,
            Source: source,
            Confidence: confidence,
            Evidence: evidence,
            SignatureHash: ComputeSignatureHash(phraseBars),
            UserCorrected: userCorrected);
    }

    private static string ComputeSignatureHash(IReadOnlyList<BarFingerprint> phraseBars)
    {
        if (phraseBars.Count == 0)
        {
            return Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();
        }

        var dimensions = phraseBars[0].Features.Count;
        var sum = new double[dimensions];

        foreach (var bar in phraseBars)
        {
            for (var d = 0; d < dimensions; d++)
            {
                sum[d] += bar.Features[d];
            }
        }

        var canonical = new StringBuilder();
        for (var d = 0; d < dimensions; d++)
        {
            canonical.Append((sum[d] / phraseBars.Count).ToString("F6")).Append(',');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }
}
