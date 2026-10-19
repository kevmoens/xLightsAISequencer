using System.Buffers.Binary;
using System.Security.Cryptography;

namespace XlightsAI.Studio.Distribution;

/// <summary>
/// Real <see cref="IUpdateVerifier"/> using RSA-SHA256 (<see cref="ReleaseManifest.SignatureAlgorithm"/>'s
/// declared value in this phase). <see cref="SignManifest"/> is the companion, release-tooling-side helper
/// that produces the self-contained container <see cref="IUpdateVerifier.VerifyManifest"/> expects; it is
/// not part of the end-user runtime path, but is required for the container format to be independently
/// verifiable and testable without a second, hidden out-of-band signature channel.
/// </summary>
public sealed class UpdateVerifier : IUpdateVerifier
{
    public bool VerifyManifest(ReadOnlySpan<byte> signedManifest, ReadOnlySpan<byte> trustedKey)
    {
        if (signedManifest.Length < 4 || trustedKey.IsEmpty)
        {
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(signedManifest);
        if (payloadLength < 0 || signedManifest.Length < 4 + payloadLength)
        {
            return false;
        }

        var payload = signedManifest.Slice(4, payloadLength);
        var signature = signedManifest[(4 + payloadLength)..];

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(trustedKey, out _);
            return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the raw manifest payload bytes from a container produced by <see cref="SignManifest"/>,
    /// without re-verifying its signature -- callers (e.g. <see cref="UpdateNoticeChecker"/>) must call
    /// <see cref="VerifyManifest"/> first and only trust the extracted payload once that call returned
    /// <see langword="true"/>. Throws <see cref="ArgumentException"/> for a malformed/truncated container
    /// rather than returning an empty or partial payload silently.
    /// </summary>
    public static byte[] ExtractPayload(ReadOnlySpan<byte> signedManifest)
    {
        if (signedManifest.Length < 4)
        {
            throw new ArgumentException("Signed manifest container is too short to contain a payload length.", nameof(signedManifest));
        }

        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(signedManifest);
        if (payloadLength < 0 || signedManifest.Length < 4 + payloadLength)
        {
            throw new ArgumentException("Signed manifest container's declared payload length is invalid.", nameof(signedManifest));
        }

        return signedManifest.Slice(4, payloadLength).ToArray();
    }

    /// <summary>
    /// Produces the self-contained container <see cref="VerifyManifest"/> expects: a 4-byte big-endian
    /// payload length, the raw <paramref name="manifestPayload"/> bytes, then the RSA-SHA256 signature.
    /// </summary>
    public static byte[] SignManifest(ReadOnlySpan<byte> manifestPayload, RSA signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);

        var signature = signingKey.SignData(manifestPayload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var container = new byte[4 + manifestPayload.Length + signature.Length];
        BinaryPrimitives.WriteInt32BigEndian(container, manifestPayload.Length);
        manifestPayload.CopyTo(container.AsSpan(4));
        signature.CopyTo(container.AsSpan(4 + manifestPayload.Length));
        return container;
    }
}
