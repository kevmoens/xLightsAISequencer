using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// PHASE-03 (Analysis extensions) Task 1's own persisted, versioned output: per-band
/// (bass/mid/high) spectral-flux-derived energy, queryable per time window, exposed as a first-class
/// analysis artifact rather than trapped as an internal implementation detail of
/// <see cref="OnsetDetector"/>. Mirrors <see cref="EnergyEnvelope"/>'s own provenance shape exactly.
/// This feeds frequency-split prop routing and labelled timing tracks (kick/snare/hat) in later phases.
/// </summary>
/// <param name="VersionId">Identity of this particular computed run -- changes every time the envelope is rebuilt, even if the resulting frames are numerically identical.</param>
/// <param name="SongId">The <c>SongAsset.Id</c> this envelope was computed from.</param>
/// <param name="SourceHash">The source <c>SongAsset.Sha256</c> at analysis time.</param>
/// <param name="SourceDecoderVersion">The <c>SongAsset.DecoderVersion</c> at analysis time.</param>
/// <param name="AnalyzerVersion">Identifies which analyzer (and its version) produced this envelope.</param>
/// <param name="Settings">The exact <see cref="BandEnergySettings"/> this envelope's windows/band cutoffs were computed with.</param>
/// <param name="Frames">Contiguous, gap-free, half-open frames covering <c>[0, SampleCount)</c> of the source song at the configured hop, ordered by <see cref="BandEnergyFrame.StartSample"/>.</param>
public sealed record BandEnergyEnvelope(
    Guid VersionId,
    SongId SongId,
    string SourceHash,
    string SourceDecoderVersion,
    string AnalyzerVersion,
    BandEnergySettings Settings,
    BandEnergyFrame[] Frames)
{
    /// <summary>
    /// Averages <see cref="BandEnergyFrame.Bass"/>/<see cref="Mid"/>/<see cref="High"/> across every
    /// frame overlapping the half-open <c>[startTicks, endTicks)</c> window -- the query surface the
    /// Task 1 acceptance criterion ("per-band energy is queryable per time window") requires. Returns
    /// <c>(0, 0, 0)</c> for a window with no overlapping frames (an empty envelope, or a window entirely
    /// outside the analyzed song) rather than throwing -- the same "silence is a valid, inspectable
    /// result" convention this codebase uses throughout.
    /// </summary>
    public (double Bass, double Mid, double High) AverageInWindow(long startTicks, long endTicks)
    {
        const long TicksPerSecond = 10_000_000L;
        var sampleRate = Settings.Rhythm.AnalysisSampleRate;

        double bassSum = 0, midSum = 0, highSum = 0;
        var count = 0;

        foreach (var frame in Frames)
        {
            var frameStartTicks = sampleRate > 0 ? frame.StartSample * TicksPerSecond / sampleRate : 0;
            var frameEndTicks = sampleRate > 0 ? frame.EndSample * TicksPerSecond / sampleRate : 0;

            if (frameEndTicks <= startTicks || frameStartTicks >= endTicks)
            {
                continue;
            }

            bassSum += frame.Bass;
            midSum += frame.Mid;
            highSum += frame.High;
            count++;
        }

        return count == 0 ? (0.0, 0.0, 0.0) : (bassSum / count, midSum / count, highSum / count);
    }
}
