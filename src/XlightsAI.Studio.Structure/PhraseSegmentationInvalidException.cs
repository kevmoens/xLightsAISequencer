using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Structure;

/// <summary>
/// Thrown when an <see cref="IPhraseSegmenter"/> run cannot proceed, or cannot persist its result,
/// because its inputs, its own computed phrase list, or a requested manual boundary correction is invalid
/// -- missing media, a mismatched/stale provenance chain, an unusable <see cref="PhraseSegmentationSettings"/>,
/// a malformed computed phrase list, or <see cref="IPhraseSegmenter.SetManualBoundariesAsync"/> addressing
/// a <see cref="SectionLabelCandidate.Id"/> that does not exist, or supplying boundary bar indices that do
/// not exist, are not strictly increasing, or fall outside the targeted section's own bar range. Step
/// 055's own failure/edge case, verbatim: "Invalid input, cancellation or stale persisted state must not
/// corrupt the project" -- caught before any cache or override file is written, mirroring
/// <see cref="SectionLabelInvalidException"/>'s own "never silently cache as if correct" rule.
/// </summary>
public sealed class PhraseSegmentationInvalidException : Exception
{
    public PhraseSegmentationInvalidException(SongId songId, string reason)
        : base($"Cannot build phrase segmentation analysis for song '{songId.Value}': {reason}")
    {
        SongId = songId;
        Reason = reason;
    }

    public SongId SongId { get; }

    public string Reason { get; }
}
