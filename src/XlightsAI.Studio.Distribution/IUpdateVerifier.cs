namespace XlightsAI.Studio.Distribution;

/// <summary>
/// PHASE-15 Section 3's own proposed signature, kept exactly as specified (two parameters: the signed
/// manifest container and the trusted key -- no separate detached-signature parameter is added here).
/// Realizes "signed/unsigned-labeled" (this step's own title, verbatim) and phase test matrix row 7,
/// "Invalid update manifest/signature rejected". <paramref name="signedManifest"/> is a self-contained
/// container produced by <see cref="UpdateVerifier.SignManifest"/>: a 4-byte big-endian payload length,
/// the raw manifest payload bytes, then the RSA-SHA256 signature bytes -- so this interface's own two
/// parameters stay exactly as proposed while still being independently verifiable. A caller that cannot
/// produce a valid signed container must label the build/update as unsigned rather than fabricate or skip
/// verification -- this interface never trusts an unsigned manifest implicitly.
/// </summary>
public interface IUpdateVerifier
{
    /// <summary>
    /// Returns <see langword="false"/> for a tampered manifest, a signature produced by a different key, a
    /// malformed/truncated container, or an empty/invalid key -- never throws for those cases, so a caller
    /// can safely show a rejection message without crashing (Section 2: "detect actual versions/capabilities",
    /// applied here to update trust).
    /// </summary>
    bool VerifyManifest(ReadOnlySpan<byte> signedManifest, ReadOnlySpan<byte> trustedKey);
}
