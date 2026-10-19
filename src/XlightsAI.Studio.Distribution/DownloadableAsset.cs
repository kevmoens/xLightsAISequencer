namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Section 3's own proposed signature, kept exactly as specified. One declared, trust-rooted entry
/// in a <see cref="ReleaseManifest"/>: a published bundle file (native library, runtime asset) whose actual
/// on-disk bytes must hash to <see cref="Sha256"/> before it is trusted -- the same "declared, pinned hash is
/// the sole basis for verified, never trusted from the response/file alone" convention
/// <c>XlightsAI.Studio.Stems.ModelAsset</c> (PHASE-06 Step 039) already established for downloadable ML
/// model weights, applied here to release/native assets instead. <see cref="Required"/> distinguishes a
/// mandatory bundled file (its absence blocks startup) from an optional one (Section 1: "optionally download
/// ML models" -- realized fully by a future Step 096 asset installer; this phase only declares the shape and
/// verifies already-present required assets).
/// </summary>
/// <param name="Id">Stable identifier for this asset within its manifest.</param>
/// <param name="Version">Pinned version/revision this declared entry describes.</param>
/// <param name="Url">Absolute source location the verified bytes are (or were) obtained from.</param>
/// <param name="Sha256">Declared, pinned SHA-256 of the full file's bytes, lower-case hex.</param>
/// <param name="SizeBytes">Declared file size in bytes, used as a cheap pre-hash sanity check.</param>
/// <param name="LicenseId">SPDX-style license identifier for this asset.</param>
/// <param name="Required">Whether this asset's absence blocks startup (<see langword="true"/>) or only disables a dependent feature (<see langword="false"/>).</param>
public sealed record DownloadableAsset(
    string Id,
    string Version,
    Uri Url,
    string Sha256,
    long SizeBytes,
    string LicenseId,
    bool Required);
