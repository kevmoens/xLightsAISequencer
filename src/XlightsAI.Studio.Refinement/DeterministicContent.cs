using System.Security.Cryptography;
using System.Text;

namespace XlightsAI.Studio.Refinement;

/// <summary>
/// PHASE-12 Step 080 ("Build seeded candidate generator constrained to selected gesture family"), this
/// step's own required acceptance, verbatim: "Same seed + inputs yields same structured candidate."
/// A deterministic candidate generator must never derive an identity or hash from
/// <see cref="Guid.NewGuid"/>, wall-clock time, <c>object.GetHashCode()</c> (which is not stable across
/// processes for strings) or set/dictionary enumeration order -- every value this type produces is a
/// pure function of the caller-supplied, already-deterministic input strings alone.
/// </summary>
internal static class DeterministicContent
{
    /// <summary>A stable, lowercase hex SHA-256 digest of <paramref name="canonicalInput"/>. Identical input always yields an identical result, in this process or any other.</summary>
    public static string Hash(string canonicalInput)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>A stable <see cref="Guid"/> derived from the first 16 bytes of <see cref="Hash(string)"/>'s own digest -- used in place of <see cref="Guid.NewGuid"/> wherever a generated identity must itself be reproducible.</summary>
    public static Guid GuidFrom(string canonicalInput)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
