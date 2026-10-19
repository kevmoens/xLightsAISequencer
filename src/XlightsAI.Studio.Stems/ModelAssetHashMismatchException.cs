namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="ModelAssetManager.EnsureVerifiedAsync"/> when a downloaded (or stale,
/// re-inspected on-disk) file's actual SHA-256 does not match the <see cref="ModelAsset"/>'s declared,
/// pinned <see cref="ModelAsset.Sha256"/>. Step 039's own required acceptance, verbatim: "Tampered models
/// rejected". The mismatched file is never promoted to the verified cache path.
/// </summary>
public sealed class ModelAssetHashMismatchException : Exception
{
    public ModelAssetHashMismatchException(string modelAssetId, string expectedSha256, string actualSha256)
        : base($"Model asset '{modelAssetId}' failed checksum validation: expected sha256 '{expectedSha256}' but got '{actualSha256}'. The asset was rejected and not cached.")
    {
    }
}
