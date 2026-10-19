namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Section 3's own proposed signature, kept exactly as specified. The full set of declared assets
/// a given published release/channel carries, plus the algorithm its own signature (see
/// <see cref="IUpdateVerifier"/>) was produced with.
/// </summary>
/// <param name="ProductVersion">The published product version this manifest describes.</param>
/// <param name="Channel">Release channel, e.g. "stable" or "beta".</param>
/// <param name="Assets">Every declared asset this release's bundle depends on or offers.</param>
/// <param name="SignatureAlgorithm">Name of the signature algorithm an accompanying detached signature was produced with (e.g. "RSA-SHA256").</param>
public sealed record ReleaseManifest(
    string ProductVersion,
    string Channel,
    IReadOnlyList<DownloadableAsset> Assets,
    string SignatureAlgorithm);
