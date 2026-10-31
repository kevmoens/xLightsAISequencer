using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Distribution;
using XlightsAI.Studio.Knowledge;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;
using XlightsAI.Studio.Structure;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// PHASE-2b (build plan §2b.7; architecture §8b): runs real audio analysis — onset/tempo/beat-grid,
/// bar fingerprints, section boundaries/labels, phrase segmentation — against one
/// <see cref="CorpusEntry"/>'s paired audio, parses its <c>.xsq</c> via <see cref="EffectAlignmentParser"/>,
/// and aligns every parsed effect onto the resulting grid.
///
/// <b>Real-root-cause fix (Phase 7 harness follow-up):</b> a real full-length song analysis confirmed
/// bar fingerprints built from a zero-chroma melody collapse <see cref="SectionBoundaryDetector"/>'s
/// adjacent-bar cosine novelty onto energy alone, which never clears its threshold on a continuously
/// loud, harmonically-flat track — a song never segments past one section regardless of length
/// (see <c>implementation-evidence/PHASE-07-HARNESS.md</c>, "Full-length song attempt"). Running full
/// <c>IStemSeparator</c> source separation purely to get a melody for structure analysis was judged too
/// large/risky a dependency for this fix (it needs a real downloadable ONNX model + explicit user
/// consent, unverified against real model weights anywhere in this repo's own test suite — see
/// <see cref="Stems.OnnxStemSeparator"/>'s consent-gated design). Instead, this runs the same real
/// <see cref="PitchTracker"/>/<see cref="PitchFrameNoteSegmenter"/>/<see cref="MelodicContourClassifier"/>
/// chain <see cref="AudioAnalysisRunner"/> uses, but directly against the full decoded mix (wrapped as a
/// <see cref="StemAsset"/> of <see cref="StemKind.Other"/>, never separated) instead of an isolated lead
/// stem — the same "fallback ladder" principle <c>docs/phase-specs/PHASE-07.md</c> §3 already states for
/// melody analysis generally ("trustworthy MIDI -&gt; high-confidence isolated-stem note events -&gt;
/// pitch contour direction and onset"): reduced fidelity on dense polyphonic mixes, but a real, non-
/// fabricated pitch signal — never a silently-empty one — which is enough to give
/// <see cref="BarFingerprintBuilder"/> genuine chroma variation to key its novelty detection on. Because
/// no separation occurs, this melody's own <see cref="MelodyTrack.SourceAssetHash"/> is exactly the
/// song's own hash, so it naturally satisfies every context's source-hash check without needing
/// <see cref="StructureAnalysisRunner"/>'s separate <c>MelodySourceAssetHash</c> plumbing.
/// </summary>
public sealed class CorpusSongAnalyzer
{
    private readonly string _cacheRootDirectory;
    private readonly IAudioDecoder _decoder;

    public CorpusSongAnalyzer(string cacheRootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRootDirectory);
        _cacheRootDirectory = cacheRootDirectory;

        var wavCacheDirectory = Path.Combine(cacheRootDirectory, "wav-cache");
        _decoder = new TranscodingAudioDecoder(new WavAudioDecoder(), new LocalPathFfmpegInstaller(), wavCacheDirectory);
    }

    public async Task<CorpusSongAnalysis> AnalyzeAsync(CorpusEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var song = await _decoder.ProbeAsync(entry.AudioPath, ct).ConfigureAwait(false);
        var settings = AudioAnalysisSettingsBundle.ForSampleRate(song.SampleRate);

        var onsets = await new OnsetDetector(_decoder, new JsonOnsetAnalysisStore(Dir("onsets")))
            .DetectAsync(song, settings.Rhythm, settings.Onset, ct).ConfigureAwait(false);
        var tempo = await new TempoDetector(new JsonTempoAnalysisStore(Dir("tempo")))
            .DetectAsync(onsets, settings.Tempo, ct).ConfigureAwait(false);
        var beatGrid = await new BeatGridBuilder(new JsonBeatGridStore(Dir("beat-grids")))
            .BuildAsync(onsets, tempo, settings.BeatGrid, ct).ConfigureAwait(false);
        var energy = await new EnergyEnvelopeAnalyzer(_decoder, new JsonEnergyEnvelopeStore(Dir("energy")))
            .AnalyzeAsync(song, settings.Rhythm, ct).ConfigureAwait(false);

        // See class doc comment: real pitch tracking against the full mix (never separated), not a
        // fabricated/empty melody -- gives BarFingerprintBuilder genuine chroma variation.
        var fullMixStem = new StemAsset(Guid.NewGuid(), StemKind.Other, song.FilePath, song.SampleRate, song.SampleCount, song.OriginTicks, song.Sha256);
        var pitchAnalysis = await new PitchTracker(_decoder, new JsonPitchTrackStore(Dir("pitch-tracks")))
            .TrackAsync(fullMixStem, settings.PitchTracking, ct).ConfigureAwait(false);
        var notes = PitchFrameNoteSegmenter.Segment(pitchAnalysis, settings.NoteSegmentation);
        var melody = MelodicContourClassifier.Classify(new MelodyTrack(Guid.NewGuid(), song.Sha256, notes, []), settings.GestureClassification);

        var fingerprintSettings = StructureAnalysisSettingsBundle.Default;

        var fingerprints = await new BarFingerprintBuilder(new JsonBarFingerprintStore(Dir("bar-fingerprints")))
            .BuildAsync(new BarFingerprintContext(song.Id, song.Sha256, beatGrid, energy, melody), fingerprintSettings.BarFingerprint, ct)
            .ConfigureAwait(false);

        var boundaries = await new SectionBoundaryDetector(new JsonSectionBoundaryStore(Dir("section-boundaries")))
            .DetectAsync(new SectionBoundaryContext(song.Id, song.Sha256, fingerprints), fingerprintSettings.SectionBoundary, ct)
            .ConfigureAwait(false);

        var labels = await new SectionLabelClassifier(new JsonSectionLabelStore(Dir("section-labels")), new JsonSectionLabelOverrideStore(Dir("section-label-overrides")))
            .ClassifyAsync(new SectionLabelContext(song.Id, song.Sha256, boundaries, fingerprints), fingerprintSettings.SectionLabel, ct)
            .ConfigureAwait(false);

        // PHASE-03 (Analysis extensions) Task 3: fit TargetPhraseLengthBars statistically per song rather
        // than using the fixed default, same as StructureAnalysisRunner.
        var targetPhraseLengthBars = PhrasePeriodEstimator.EstimateTargetPhraseLengthBars(fingerprints.Bars);
        var phraseSettings = fingerprintSettings.PhraseSegmentation with { TargetPhraseLengthBars = targetPhraseLengthBars };

        // melody.SourceAssetHash == song.Sha256 here (full mix, never separated -- see class doc
        // comment); MelodySourceAssetHash is required whenever a MelodyTrack is supplied at all.
        var phrases = await new PhraseSegmenter(new JsonPhraseSegmentationStore(Dir("phrases")), new JsonPhraseBoundaryOverrideStore(Dir("phrase-overrides")))
            .SegmentAsync(new PhraseSegmentationContext(song.Id, song.Sha256, labels, fingerprints, melody, melody.SourceAssetHash), phraseSettings, ct)
            .ConfigureAwait(false);

        var parsedEffects = EffectAlignmentParser.Parse(entry.SequencePath);
        var aligned = EffectAligner.Align(parsedEffects, beatGrid, fingerprints, labels, phrases);

        return new CorpusSongAnalysis(entry, beatGrid, fingerprints, labels, phrases, aligned);
    }

    private string Dir(string name) => Path.Combine(_cacheRootDirectory, name);
}
