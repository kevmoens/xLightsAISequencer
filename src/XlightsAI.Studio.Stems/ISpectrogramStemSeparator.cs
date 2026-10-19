using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>
/// Phase-local sibling to <see cref="IStemSeparator"/>. Disclosed, deliberate: <see cref="IStemSeparator.SeparateAsync"/>
/// takes exactly one <see cref="ModelAsset"/> for one multi-stem-output model; the real
/// sherpa-onnx-spleeter-2stems candidate is two separate single-stem models sharing one archive, which is
/// not the shape <see cref="IStemSeparator"/>'s signature or <see cref="OnnxStemSeparator"/>'s single-
/// <see cref="Microsoft.ML.OnnxRuntime.InferenceSession"/> internals can express without a breaking change
/// to an already-shipped, already-tested Phase 06 contract. This sibling interface still returns the exact
/// same canonical <see cref="StemManifest"/>/<see cref="StemAsset"/> shape Phase 06 Section 3 already owns
/// -- only the input shape differs, never the output contract downstream code (mixer UI, activity
/// detection, quality evaluation) already depends on.
/// </summary>
public interface ISpectrogramStemSeparator
{
    /// <exception cref="StemSeparationIncompatibleSourceException"><paramref name="source"/> is not 44100 Hz stereo.</exception>
    /// <exception cref="StemSeparationOutputShapeException">Either session's output tensor shape is incompatible with the configured recipe.</exception>
    /// <exception cref="ModelAssetConsentRequiredException">The archive is not already verified locally and the caller has not separately granted download consent.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<StemManifest> SeparateAsync(
        SongAsset source,
        SpectrogramModelBundle modelBundle,
        SpectrogramSeparationRecipe recipe,
        IProgress<SeparationProgress>? progress,
        CancellationToken ct);
}
