using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XlightsAI.Studio.Audio;
using XlightsAI.Studio.Melody;
using XlightsAI.Studio.Rhythm;
using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Chains the real, previously-disconnected analysis stages this plan found never joined in any
/// production code: stem separation (<see cref="Stems.IStemSeparator"/>) feeds the isolated lead stem
/// to Melody's pitch tracker, while Rhythm's onset/tempo/beat-grid and energy-envelope analyzers run
/// directly against the full mix. <see cref="PitchFrameNoteSegmenter"/> (new to this pipeline — see its
/// own doc comment) bridges Melody's continuous pitch contour into the discrete notes
/// <c>MelodicContourClassifier</c> needs.
///
/// Never grants stem-separation model-download consent itself: <see cref="Stems.OnnxStemSeparator"/>
/// always calls its <c>IModelAssetManager</c> with <c>userConsent: false</c>, so a not-yet-installed
/// model surfaces as <see cref="Stems.ModelAssetConsentRequiredException"/>, which this method lets
/// propagate unchanged for the UI layer to handle with an explicit "Download separation model" action —
/// the same "never a hidden download" convention already used for ffmpeg (<c>IFfmpegInstaller</c>).
/// </summary>
public sealed class AudioAnalysisRunner
{
    private readonly IAudioDecoder _decoder;
    private readonly IStemSeparator _stemSeparator;
    private readonly ModelAsset _stemSeparationModel;
    private readonly IOnsetAnalysisStore _onsetStore;
    private readonly ITempoAnalysisStore _tempoStore;
    private readonly IBeatGridStore _beatGridStore;
    private readonly IEnergyEnvelopeStore _energyStore;
    private readonly IPitchTrackStore _pitchTrackStore;
    private readonly IStemActivityResultStore? _vocalActivityStore;
    private readonly IBandEnergyEnvelopeStore? _bandEnergyStore;
    private readonly ILogger<AudioAnalysisRunner> _logger;

    /// <summary>Same 125ms window <c>VisemeApproximatorTests</c> already establishes for this analyzer.</summary>
    private static readonly TimeSpan VisemeWindowSize = TimeSpan.FromMilliseconds(125);

    public AudioAnalysisRunner(
        IAudioDecoder decoder,
        IStemSeparator stemSeparator,
        ModelAsset stemSeparationModel,
        IOnsetAnalysisStore onsetStore,
        ITempoAnalysisStore tempoStore,
        IBeatGridStore beatGridStore,
        IEnergyEnvelopeStore energyStore,
        IPitchTrackStore pitchTrackStore,
        IStemActivityResultStore? vocalActivityStore = null,
        IBandEnergyEnvelopeStore? bandEnergyStore = null,
        ILogger<AudioAnalysisRunner>? logger = null)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _stemSeparator = stemSeparator ?? throw new ArgumentNullException(nameof(stemSeparator));
        _stemSeparationModel = stemSeparationModel ?? throw new ArgumentNullException(nameof(stemSeparationModel));
        _onsetStore = onsetStore ?? throw new ArgumentNullException(nameof(onsetStore));
        _tempoStore = tempoStore ?? throw new ArgumentNullException(nameof(tempoStore));
        _beatGridStore = beatGridStore ?? throw new ArgumentNullException(nameof(beatGridStore));
        _energyStore = energyStore ?? throw new ArgumentNullException(nameof(energyStore));
        _pitchTrackStore = pitchTrackStore ?? throw new ArgumentNullException(nameof(pitchTrackStore));
        _vocalActivityStore = vocalActivityStore;
        _bandEnergyStore = bandEnergyStore;
        _logger = logger ?? NullLogger<AudioAnalysisRunner>.Instance;
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(SongAsset song, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(song);

        var overall = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Audio analysis starting for song {SongId} ({SampleRate}Hz).", song.Id, song.SampleRate);

        var settings = AudioAnalysisSettingsBundle.ForSampleRate(song.SampleRate);

        var stems = await Step("stem-separation", () => _stemSeparator.SeparateAsync(song, _stemSeparationModel, progress: null, ct)).ConfigureAwait(false);
        var leadStem = SelectLeadStem(stems);
        _logger.LogDebug("Selected lead stem {StemKind} ({StemSha256}).", leadStem.Kind, leadStem.Sha256);

        var onsets = await Step("onset-detection", () => new OnsetDetector(_decoder, _onsetStore)
            .DetectAsync(song, settings.Rhythm, settings.Onset, ct)).ConfigureAwait(false);
        var tempo = await Step("tempo-detection", () => new TempoDetector(_tempoStore)
            .DetectAsync(onsets, settings.Tempo, ct)).ConfigureAwait(false);
        var beatGrid = await Step("beat-grid", () => new BeatGridBuilder(_beatGridStore)
            .BuildAsync(onsets, tempo, settings.BeatGrid, ct)).ConfigureAwait(false);
        var energy = await Step("energy-envelope", () => new EnergyEnvelopeAnalyzer(_decoder, _energyStore)
            .AnalyzeAsync(song, settings.Rhythm, ct)).ConfigureAwait(false);

        var pitchAnalysis = await Step("pitch-tracking", () => new PitchTracker(_decoder, _pitchTrackStore)
            .TrackAsync(leadStem, settings.PitchTracking, ct)).ConfigureAwait(false);
        var notes = PitchFrameNoteSegmenter.Segment(pitchAnalysis, settings.NoteSegmentation);
        var rawTrack = new MelodyTrack(Guid.NewGuid(), leadStem.Sha256, notes, []);
        var melody = MelodicContourClassifier.Classify(rawTrack, settings.GestureClassification);
        _logger.LogDebug("Segmented {NoteCount} notes from the pitch contour.", notes.Count);

        var vocalActivity = await Step("vocal-activity", () => new VocalActivityAnalyzer(_decoder, _vocalActivityStore)
            .AnalyzeAsync(stems, ct)).ConfigureAwait(false);
        var bandEnergy = await Step("band-energy", () => new BandEnergyAnalyzer(_decoder, _bandEnergyStore)
            .AnalyzeAsync(song, settings.BandEnergy, ct)).ConfigureAwait(false);
        var visemeMarks = await Step("viseme-approximation", () => new VisemeApproximator(_decoder)
            .ApproximateAsync(stems, vocalActivity, VisemeWindowSize, ct)).ConfigureAwait(false);

        _logger.LogInformation("Audio analysis completed for song {SongId} in {ElapsedMs}ms.", song.Id, overall.ElapsedMilliseconds);
        return new AudioAnalysisResult(stems, beatGrid, energy, melody, vocalActivity, bandEnergy, visemeMarks, Onsets: onsets);

        async Task<T> Step<T>(string name, Func<Task<T>> action)
        {
            var stepWatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogDebug("Audio analysis step '{Step}' starting.", name);
            try
            {
                var result = await action().ConfigureAwait(false);
                _logger.LogDebug("Audio analysis step '{Step}' completed in {ElapsedMs}ms.", name, stepWatch.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audio analysis step '{Step}' failed after {ElapsedMs}ms.", name, stepWatch.ElapsedMilliseconds);
                throw;
            }
        }
    }

    /// <summary>
    /// Deterministic heuristic (same philosophy as this plan's creative-direction/role-assignment
    /// heuristics): prefer an isolated vocal stem as the melodic lead; fall back to "Other" (demucs-style
    /// separators route unclassified melodic content — lead synth, strings, etc. — here for an
    /// instrumental track with no vocals); fall back to the first produced stem if even that is absent,
    /// rather than throwing and blocking the whole analysis over a stem-labeling edge case.
    /// </summary>
    private static StemAsset SelectLeadStem(StemManifest stems)
    {
        if (stems.Stems.Count == 0)
        {
            throw new InvalidOperationException(
                $"Stem separation run '{stems.RunId}' produced zero stems; cannot select a melodic lead.");
        }

        return stems.Stems.FirstOrDefault(s => s.Kind == StemKind.Vocals)
            ?? stems.Stems.FirstOrDefault(s => s.Kind == StemKind.Other)
            ?? stems.Stems[0];
    }
}
