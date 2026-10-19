using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Step 037's own export-time boundary: an already-computed <see cref="BeatGridQualityReport"/>'s
/// warnings must actually prevent an indiscriminate full-song export, not merely be a value a caller
/// could choose to ignore — a report nobody is forced to act on is not really a gate. A deliberately
/// narrower, partial-range export (e.g. a scrubbed preview over a selected scope) is always allowed
/// regardless of confidence; only a literal, full-song export is gated, matching this step's own
/// implementation guidance, verbatim: "false positives must show warning, not trigger a blind
/// whole-song export."
/// </summary>
public interface IRhythmExportGate
{
    /// <summary>
    /// Allows the export to proceed, or throws, based on whether <paramref name="requestedRange"/>
    /// exactly covers <paramref name="fullSongRange"/> while <paramref name="report"/> is flagged
    /// low-confidence and <paramref name="lowConfidenceAcknowledged"/> is <see langword="false"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="RhythmExportBlockedException">
    /// <paramref name="requestedRange"/> == <paramref name="fullSongRange"/>, <see cref="BeatGridQualityReport.IsLowConfidence"/>
    /// is <see langword="true"/>, and <paramref name="lowConfidenceAcknowledged"/> is <see langword="false"/>.
    /// </exception>
    void EnsureExportAllowed(SongId songId, BeatGridQualityReport report, Contracts.TimeWindow requestedRange, Contracts.TimeWindow fullSongRange, bool lowConfidenceAcknowledged);
}
