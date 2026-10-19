using XlightsAI.Studio.Audio;

namespace XlightsAI.Studio.Stems;

/// <summary>PHASE-06 Section 3's own proposed signature, implemented for real by Step 040.</summary>
public interface IStemSeparator
{
    /// <summary>
    /// Separates <paramref name="source"/> into stems using <paramref name="model"/>, resolved/verified
    /// via the configured <see cref="IModelAssetManager"/>.
    /// </summary>
    /// <exception cref="StemSeparationIncompatibleSourceException"><paramref name="source"/>'s sample rate/channel count does not match the configured recipe.</exception>
    /// <exception cref="StemSeparationOutputShapeException">The loaded model's output tensor shape is incompatible with the configured recipe.</exception>
    /// <exception cref="Stems.ModelAssetConsentRequiredException">The model is not already verified locally and the caller has not separately granted download consent.</exception>
    /// <exception cref="Stems.ModelAssetHashMismatchException">A re-downloaded/stale model file no longer matches its declared hash.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
    Task<StemManifest> SeparateAsync(
        SongAsset source, ModelAsset model, IProgress<SeparationProgress>? progress, CancellationToken ct);
}
