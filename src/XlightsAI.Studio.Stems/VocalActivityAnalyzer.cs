using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 2 -- real implementation of <see cref="IVocalActivityAnalyzer"/>:
/// finds the <see cref="StemKind.Vocals"/> entry in a separated <see cref="StemManifest"/> (when present)
/// and runs the already-real, already-tested <see cref="StemActivityDetector"/> against it with
/// <see cref="VocalActivitySettings"/>'s vocal-tuned windowing, exposing the result as a first-class
/// <c>vocalActive</c> envelope instead of leaving vocal/instrumental discrimination entirely absent from
/// the analysis pipeline (the gap <c>EXPORT_AUDIT.md</c> finding #15/#8 identified: "our analysis pipeline
/// has zero ability to detect this, so vocal and instrumental passages are sequenced identically").
/// </summary>
public sealed class VocalActivityAnalyzer : IVocalActivityAnalyzer
{
    /// <summary>Burned into every <see cref="VocalActivityEnvelope.DetectorVersion"/> this analyzer produces.</summary>
    public const string Version = "XlightsAI.Studio.Stems.VocalActivityAnalyzer/1.0.0";

    private readonly IAudioDecoder _decoder;
    private readonly IStemActivityResultStore? _store;

    public VocalActivityAnalyzer(IAudioDecoder decoder, IStemActivityResultStore? store = null)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _store = store;
    }

    public async Task<VocalActivityEnvelope> AnalyzeAsync(StemManifest stems, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stems);
        ct.ThrowIfCancellationRequested();

        var vocalsStem = stems.Stems.FirstOrDefault(s => s.Kind == StemKind.Vocals);
        if (vocalsStem is null)
        {
            // No vocals stem in this manifest (instrumental-only separation, or a separator that never
            // populated StemKind.Vocals) -- an explicit, non-error "nothing to flag" result, per this
            // type's own documented tolerance of the six-member enum's two-in-practice reality.
            return new VocalActivityEnvelope(
                Guid.NewGuid(), VocalsStemId: null, SourceHash: null, Version,
                Settings: VocalActivitySettings.ForSampleRate(44_100), ActiveRegions: []);
        }

        var settings = VocalActivitySettings.ForSampleRate(vocalsStem.SampleRate);
        var detector = new StemActivityDetector(_decoder, settings, _store);
        var regions = await detector.DetectAsync(vocalsStem, ct).ConfigureAwait(false);

        return new VocalActivityEnvelope(
            Guid.NewGuid(), vocalsStem.Id, vocalsStem.Sha256, Version, settings, regions);
    }
}
