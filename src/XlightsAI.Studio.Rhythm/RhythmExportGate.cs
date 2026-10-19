using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>PHASE-05 Step 037 — real implementation of <see cref="IRhythmExportGate"/>. See the interface's own remarks for the overall design.</summary>
public sealed class RhythmExportGate : IRhythmExportGate
{
    public void EnsureExportAllowed(SongId songId, BeatGridQualityReport report, TimeWindow requestedRange, TimeWindow fullSongRange, bool lowConfidenceAcknowledged)
    {
        ArgumentNullException.ThrowIfNull(report);

        var isFullSongExport = requestedRange.StartTicks == fullSongRange.StartTicks
            && requestedRange.EndTicks == fullSongRange.EndTicks;

        if (isFullSongExport && report.IsLowConfidence && !lowConfidenceAcknowledged)
        {
            throw new RhythmExportBlockedException(songId, report.Warnings);
        }
    }
}
