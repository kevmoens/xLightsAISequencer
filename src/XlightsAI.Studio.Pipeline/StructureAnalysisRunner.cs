using XlightsAI.Studio.Contracts;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Chains Structure's real, individually-tested analysis stages — each already consumes the previous
/// stage's exact output type directly (<see cref="BarFingerprintContext"/> takes Rhythm's
/// <see cref="BeatGrid"/>/<see cref="EnergyEnvelope"/> and Melody's <see cref="MelodyTrack"/> by direct
/// reference, confirmed before writing this), so this runner's own job is purely sequencing real
/// production types with no new adapter/bridge code, unlike <see cref="AudioAnalysisRunner"/>.
/// <see cref="IChoreographyTemplatePlanner"/> is deliberately excluded from this chain: it requires an
/// already-authored source phrase's own effects to define a template from (a human-editing feature, not an
/// automatic-generation step), so it has no place in an automatic analysis pipeline.
///
/// <b>PHASE-03 (Analysis extensions) Task 4 fix:</b> <see cref="PhraseSegmenter"/> used to require an
/// exact <c>MelodyTrack.SourceAssetHash == SourceHash</c> match to use melodic-cadence snapping at all,
/// which could never be satisfied for a real <c>MelodyTrack</c> — <see cref="AudioAnalysisRunner"/>'s own
/// melody is derived from an isolated lead *stem*, whose hash is legitimately never the full song's own
/// hash (that is the stem's own correct cache-key identity, not a bug). This runner previously papered
/// over that mismatch by passing <see langword="null"/> into phrase segmentation instead, silently losing
/// cadence-aware phrasing. <see cref="PhraseSegmentationContext.MelodySourceAssetHash"/> now threads the
/// melody's *real* expected hash (<paramref name="melody"/>'s own <c>SourceAssetHash</c>) through
/// separately from <paramref name="sourceHash"/>, so <see cref="PhraseSegmenter"/> validates the melody
/// against the provenance it was actually built from instead of a value it can never equal — cadence
/// snapping now actually runs against the real, stem-derived melody.
/// </summary>
public sealed class StructureAnalysisRunner
{
    private readonly IBarFingerprintStore _fingerprintStore;
    private readonly ISectionBoundaryStore _boundaryStore;
    private readonly ISectionLabelStore _labelStore;
    private readonly ISectionLabelOverrideStore _labelOverrideStore;
    private readonly IPhraseSegmentationStore _phraseStore;
    private readonly IPhraseBoundaryOverrideStore _phraseOverrideStore;
    private readonly IMotifClusteringStore _motifStore;

    public StructureAnalysisRunner(
        IBarFingerprintStore fingerprintStore,
        ISectionBoundaryStore boundaryStore,
        ISectionLabelStore labelStore,
        ISectionLabelOverrideStore labelOverrideStore,
        IPhraseSegmentationStore phraseStore,
        IPhraseBoundaryOverrideStore phraseOverrideStore,
        IMotifClusteringStore motifStore)
    {
        _fingerprintStore = fingerprintStore ?? throw new ArgumentNullException(nameof(fingerprintStore));
        _boundaryStore = boundaryStore ?? throw new ArgumentNullException(nameof(boundaryStore));
        _labelStore = labelStore ?? throw new ArgumentNullException(nameof(labelStore));
        _labelOverrideStore = labelOverrideStore ?? throw new ArgumentNullException(nameof(labelOverrideStore));
        _phraseStore = phraseStore ?? throw new ArgumentNullException(nameof(phraseStore));
        _phraseOverrideStore = phraseOverrideStore ?? throw new ArgumentNullException(nameof(phraseOverrideStore));
        _motifStore = motifStore ?? throw new ArgumentNullException(nameof(motifStore));
    }

    public async Task<StructureAnalysisResult> AnalyzeAsync(
        SongId songId, string sourceHash, BeatGrid beatGrid, EnergyEnvelope energy, MelodyTrack melody, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceHash);
        ArgumentNullException.ThrowIfNull(beatGrid);
        ArgumentNullException.ThrowIfNull(energy);
        ArgumentNullException.ThrowIfNull(melody);

        var settings = StructureAnalysisSettingsBundle.Default;

        var fingerprints = await new BarFingerprintBuilder(_fingerprintStore)
            .BuildAsync(new BarFingerprintContext(songId, sourceHash, beatGrid, energy, melody), settings.BarFingerprint, ct)
            .ConfigureAwait(false);

        var boundaries = await new SectionBoundaryDetector(_boundaryStore)
            .DetectAsync(new SectionBoundaryContext(songId, sourceHash, fingerprints), settings.SectionBoundary, ct)
            .ConfigureAwait(false);

        var labels = await new SectionLabelClassifier(_labelStore, _labelOverrideStore)
            .ClassifyAsync(new SectionLabelContext(songId, sourceHash, boundaries, fingerprints), settings.SectionLabel, ct)
            .ConfigureAwait(false);

        // PHASE-03 (Analysis extensions) Task 3: fit TargetPhraseLengthBars statistically per song
        // (MUSIC_FLOW_ANALYSIS.md's own methodology) instead of using the fixed default forever.
        var targetPhraseLengthBars = PhrasePeriodEstimator.EstimateTargetPhraseLengthBars(fingerprints.Bars);
        var phraseSettings = settings.PhraseSegmentation with { TargetPhraseLengthBars = targetPhraseLengthBars };

        // See class doc comment (Task 4 fix): thread melody's own real SourceAssetHash through as the
        // context's MelodySourceAssetHash, so PhraseSegmenter validates it against the provenance it was
        // actually built from instead of the full song's hash, which it can never equal.
        var phrases = await new PhraseSegmenter(_phraseStore, _phraseOverrideStore)
            .SegmentAsync(new PhraseSegmentationContext(songId, sourceHash, labels, fingerprints, melody, melody.SourceAssetHash), phraseSettings, ct)
            .ConfigureAwait(false);

        var motifs = await new MotifClusterer(_motifStore)
            .ClusterAsync(new MotifClusteringContext(songId, sourceHash, phrases, fingerprints), settings.MotifClustering, ct)
            .ConfigureAwait(false);

        return new StructureAnalysisResult(fingerprints, boundaries, labels, phrases, motifs);
    }
}
