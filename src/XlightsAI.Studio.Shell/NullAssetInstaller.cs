using XlightsAI.Studio.Distribution;

namespace XlightsAI.Studio.Shell;

/// <summary>
/// Throwing default <see cref="IAssetInstaller"/> used as <see cref="ShellViewModel"/>'s new Step 096
/// constructor parameter, same "optional collaborator that needs real external configuration this layer
/// does not yet own" reasoning as <see cref="NullSequenceDocumentStore"/>/<see cref="NullSongAssetStore"/> —
/// a real <see cref="XlightsAI.Studio.Distribution.AssetInstaller"/> needs a concrete install directory that
/// only a real published release's own composition root can supply, which does not exist yet at the
/// Shell/first-run layer (same disclosed deferral as <see cref="NullStartupHealthChecks"/>, PHASE-15
/// Step 095 Section 6 item 3). Unlike that non-throwing default, installing an asset is an explicit,
/// consent-gated user action (never invoked implicitly during startup), so a caller that invokes it without
/// a real collaborator configured gets a clear, disclosed failure rather than a silently-succeeding fake
/// install.
/// </summary>
public sealed class NullAssetInstaller : IAssetInstaller
{
    public static readonly NullAssetInstaller Instance = new();

    private NullAssetInstaller()
    {
    }

    public Task<string> EnsureInstalledAsync(DownloadableAsset asset, IProgress<double>? progress, CancellationToken ct)
    {
        throw new InvalidOperationException(
            "No real asset installer is configured for this composition root yet (no published release " +
            "bundle/install directory to install optional assets into).");
    }
}
