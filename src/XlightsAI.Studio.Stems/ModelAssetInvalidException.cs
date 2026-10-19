namespace XlightsAI.Studio.Stems;

/// <summary>
/// Thrown by <see cref="ModelAssetValidator.Validate"/> when a <see cref="ModelAsset"/>'s own declared
/// shape is structurally invalid (non-HTTPS source, empty identity/license/backend fields, or a
/// malformed SHA-256 digest) — rejected before any download is ever attempted.
/// </summary>
public sealed class ModelAssetInvalidException : Exception
{
    public ModelAssetInvalidException(string message) : base(message)
    {
    }
}
