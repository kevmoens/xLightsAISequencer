namespace XlightsAI.Studio.Stems;

/// <summary>
/// Transfers the bytes of a declared <see cref="ModelAsset.Source"/> to a local file. Deliberately
/// scheme-agnostic at this interface level (HTTPS-only is enforced one layer up, by
/// <see cref="ModelAssetValidator"/>, against the declared <see cref="ModelAsset"/> itself) so this
/// abstraction can be exercised directly against a real local loopback server in tests without requiring
/// a TLS certificate in this development environment.
/// </summary>
public interface IModelAssetDownloader
{
    Task DownloadAsync(Uri source, string destinationPath, CancellationToken ct);
}
