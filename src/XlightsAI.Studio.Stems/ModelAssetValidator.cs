namespace XlightsAI.Studio.Stems;

/// <summary>
/// Validates a single <see cref="ModelAsset"/>'s declared shape, shared between
/// <see cref="ModelAssetManager"/> (before any download) and <see cref="ModelAssetManifestValidator"/>
/// (before any manifest is persisted) so the same rule cannot drift between the two call sites.
/// </summary>
public static class ModelAssetValidator
{
    public static void Validate(ModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (string.IsNullOrWhiteSpace(asset.Id))
        {
            throw new ModelAssetInvalidException("ModelAsset.Id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(asset.Version))
        {
            throw new ModelAssetInvalidException("ModelAsset.Version must not be empty.");
        }

        if (asset.Source is null || !asset.Source.IsAbsoluteUri ||
            !string.Equals(asset.Source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelAssetInvalidException(
                $"ModelAsset '{asset.Id}' Source must be an absolute HTTPS URL (got '{asset.Source}').");
        }

        if (string.IsNullOrWhiteSpace(asset.Sha256) || asset.Sha256.Length != 64 ||
            !asset.Sha256.All(Uri.IsHexDigit))
        {
            throw new ModelAssetInvalidException(
                $"ModelAsset '{asset.Id}' Sha256 must be a 64-character hex SHA-256 digest.");
        }

        if (string.IsNullOrWhiteSpace(asset.LicenseId))
        {
            throw new ModelAssetInvalidException($"ModelAsset '{asset.Id}' LicenseId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(asset.InferenceBackend))
        {
            throw new ModelAssetInvalidException($"ModelAsset '{asset.Id}' InferenceBackend must not be empty.");
        }
    }
}
