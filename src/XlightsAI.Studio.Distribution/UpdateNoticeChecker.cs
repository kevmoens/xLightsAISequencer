using System.Text.Json;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IUpdateNoticeChecker"/>. A candidate manifest is only ever trusted once
/// <see cref="IUpdateVerifier.VerifyManifest"/> actually verifies its signature against
/// <paramref name="trustedKey"/> (phase test matrix row 7, "Invalid update manifest/signature rejected",
/// applied here to the update-notice path specifically, not just asset installs/startup checks); a tampered
/// signature, wrong key, or malformed/unparsable payload is reported as a clear, non-available notice, never
/// thrown.
/// </summary>
public sealed class UpdateNoticeChecker : IUpdateNoticeChecker
{
    private readonly IUpdateVerifier _verifier;

    public UpdateNoticeChecker(IUpdateVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        _verifier = verifier;
    }

    public UpdateNotice CheckForUpdate(string currentProductVersion, ReadOnlySpan<byte> signedCandidateManifest, ReadOnlySpan<byte> trustedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentProductVersion);

        if (!_verifier.VerifyManifest(signedCandidateManifest, trustedKey))
        {
            return new UpdateNotice(false, null, "Update manifest could not be verified and was ignored.");
        }

        byte[] payload;
        try
        {
            payload = UpdateVerifier.ExtractPayload(signedCandidateManifest);
        }
        catch (ArgumentException)
        {
            return new UpdateNotice(false, null, "Update manifest payload was malformed and was ignored.");
        }

        ReleaseManifest candidate;
        try
        {
            candidate = JsonSerializer.Deserialize<ReleaseManifest>(payload)
                ?? throw new JsonException("Deserialized manifest payload was null.");
        }
        catch (JsonException)
        {
            return new UpdateNotice(false, null, "Update manifest payload could not be parsed and was ignored.");
        }

        if (!IsNewer(candidate.ProductVersion, currentProductVersion))
        {
            return new UpdateNotice(false, null, $"Already up to date (current version {currentProductVersion}).");
        }

        return new UpdateNotice(
            true,
            candidate.ProductVersion,
            $"Update available: {currentProductVersion} -> {candidate.ProductVersion} ({candidate.Channel}).");
    }

    private static bool IsNewer(string candidateVersion, string currentVersion)
    {
        if (Version.TryParse(candidateVersion, out var candidate) && Version.TryParse(currentVersion, out var current))
        {
            return candidate > current;
        }

        // Neither version string parses as a dotted numeric Version -- fall back to "different means
        // newer" rather than silently reporting no update at all for a non-numeric scheme.
        return !string.Equals(candidateVersion, currentVersion, StringComparison.Ordinal);
    }
}
