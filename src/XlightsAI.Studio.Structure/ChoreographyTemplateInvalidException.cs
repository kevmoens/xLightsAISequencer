using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Thrown when an <see cref="IChoreographyTemplatePlanner"/> call cannot proceed, or cannot persist its
/// result, because its inputs are invalid -- missing media, a mismatched/stale provenance chain, a motif
/// group/phrase/template/instance identity that does not exist, a malformed override, or a caller-supplied
/// "current" analysis that no longer matches what is actually persisted. Step 057's own failure/edge case,
/// verbatim: "Invalid input, cancellation or stale persisted state must not corrupt the project" -- caught
/// before any cache file is written, mirroring <see cref="MotifClusteringInvalidException"/>'s own "never
/// silently cache as if correct" rule.
/// </summary>
public sealed class ChoreographyTemplateInvalidException : Exception
{
    public ChoreographyTemplateInvalidException(SongId songId, string reason)
        : base($"Cannot build choreography template analysis for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
