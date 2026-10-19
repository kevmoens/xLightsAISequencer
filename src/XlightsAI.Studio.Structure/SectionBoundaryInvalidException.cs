using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Thrown when an <see cref="ISectionBoundaryDetector"/> run cannot proceed, or cannot persist its
/// result, because its inputs or its own computed candidate list are invalid -- missing media, a
/// mismatched/stale provenance chain, an unusable <see cref="SectionBoundarySettings"/>, or a malformed
/// computed boundary list (non-monotonic bar indices, an out-of-range bar index, or a missing
/// start-of-song entry). Step 053's own failure/edge case, verbatim: "Malformed proposal... must not
/// auto-commit" -- caught before any cache file is written, mirroring
/// <see cref="BarFingerprintInvalidException"/>'s own "never silently cache as if correct" rule.
/// </summary>
public sealed class SectionBoundaryInvalidException : Exception
{
    public SectionBoundaryInvalidException(SongId songId, string reason)
        : base($"Cannot build section boundary analysis for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
