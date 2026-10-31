using XlightsAI.Studio.Stems;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Throwing default <see cref="IModelAssetManager"/>, same "needs a real install directory this layer
/// does not yet own" reasoning as <see cref="NullAssetInstaller"/>/<see cref="NullFfmpegInstaller"/> —
/// backs the Show Import screen's "Download separation model" action; a caller that invokes it without a
/// real composition root configured gets a clear, disclosed failure rather than a silently-failing no-op.
/// </summary>
public sealed class NullModelAssetManager : IModelAssetManager
{
    public static readonly NullModelAssetManager Instance = new();

    private NullModelAssetManager()
    {
    }

    public Task<string> EnsureVerifiedAsync(ModelAsset asset, bool userConsent, CancellationToken ct)
    {
        throw new InvalidOperationException(
            "No real model asset manager is configured for this composition root yet (no model cache directory to download/verify into).");
    }
}
