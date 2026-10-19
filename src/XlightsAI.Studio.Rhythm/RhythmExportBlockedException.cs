using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Thrown by <see cref="IRhythmExportGate.EnsureExportAllowed"/> when a requested export range exactly
/// covers the full song and the evaluating <see cref="BeatGridQualityReport"/> is flagged low-confidence
/// and has not been explicitly acknowledged — Step 037's own implementation guidance, verbatim: "false
/// positives must show warning, not trigger a blind whole-song export." The caller must either present
/// <see cref="Warnings"/> to the user and retry with acknowledgement, or scope the export to a narrower,
/// deliberately chosen range.
/// </summary>
public sealed class RhythmExportBlockedException : Exception
{
    public RhythmExportBlockedException(SongId songId, IReadOnlyList<string> warnings)
        : base($"Blocked a full-song export for song '{songId.Value}': the beat grid is flagged low-confidence and has not been acknowledged. {string.Join(" ", warnings)}")
    {
        SongId = songId;
        Warnings = warnings;
    }

    public SongId SongId { get; }

    public IReadOnlyList<string> Warnings { get; }
}
