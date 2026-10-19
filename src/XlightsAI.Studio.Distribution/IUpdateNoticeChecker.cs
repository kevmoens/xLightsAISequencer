namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Compares the currently installed product version against a candidate release's signed manifest
/// container, surfacing a non-blocking <see cref="UpdateNotice"/> rather than ever auto-applying an update
/// (Section 2: "Do not silently pull remote model updates after initial consent," applied here to app
/// releases). This step's own title, verbatim: "...and update notices."
/// </summary>
public interface IUpdateNoticeChecker
{
    /// <summary>
    /// Never throws for a tampered signature, a wrong signing key, or a malformed/unparsable manifest
    /// payload -- each is reported as a non-available, explained <see cref="UpdateNotice"/>, same
    /// "never trust, never crash" convention as <see cref="IUpdateVerifier.VerifyManifest"/>.
    /// </summary>
    UpdateNotice CheckForUpdate(string currentProductVersion, ReadOnlySpan<byte> signedCandidateManifest, ReadOnlySpan<byte> trustedKey);
}
