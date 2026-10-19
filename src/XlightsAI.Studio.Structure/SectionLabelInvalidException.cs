using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Thrown when an <see cref="ISectionLabelClassifier"/> run cannot proceed, or cannot persist its result,
/// because its inputs, its own computed candidate list, or a requested override target is invalid --
/// missing media, a mismatched/stale provenance chain, an unusable <see cref="SectionLabelSettings"/>, a
/// malformed computed segment list, or <see cref="ISectionLabelClassifier.SetOverrideAsync"/> addressing a
/// <see cref="SectionLabelCandidate.BoundaryId"/> that does not exist in the current classification. Step
/// 054's own failure/edge case, verbatim: "Malformed proposal... must not auto-commit" -- caught before
/// any cache or override file is written, mirroring <see cref="SectionBoundaryInvalidException"/>'s own
/// "never silently cache as if correct" rule.
/// </summary>
public sealed class SectionLabelInvalidException : Exception
{
    public SectionLabelInvalidException(SongId songId, string reason)
        : base($"Cannot build section label analysis for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
