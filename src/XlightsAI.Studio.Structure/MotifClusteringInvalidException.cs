using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Thrown when an <see cref="IMotifClusterer"/> run cannot proceed, or cannot persist its result, because
/// its inputs or its own computed group/link list are invalid -- missing media, a mismatched/stale
/// provenance chain, an unusable <see cref="MotifClusteringSettings"/>, or a phrase whose bar range
/// references a bar that does not exist in the supplied <see cref="SongFingerprintSet"/>. Step 056's own
/// failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not corrupt the
/// project" -- caught before any cache file is written, mirroring
/// <see cref="PhraseSegmentationInvalidException"/>'s own "never silently cache as if correct" rule.
/// </summary>
public sealed class MotifClusteringInvalidException : Exception
{
    public MotifClusteringInvalidException(SongId songId, string reason)
        : base($"Cannot build motif clustering analysis for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
