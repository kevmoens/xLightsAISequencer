namespace XlightsAI.Studio.Stems;

/// <summary>
/// Sample-rate-derived default <see cref="StemActivityDetectionSettings"/> for <see cref="VocalActivityAnalyzer"/>,
/// tuned to the methodology <c>EXPORT_AUDIT.md</c>'s own vocals investigation already validated against a
/// real track: ~2-second RMS windows (long enough to ride through the ~0.3-0.5s syllabic pulsing within one
/// cluster without flickering active/inactive note-to-note), with hysteresis thresholds chosen so that only
/// windows "well above baseline" (that investigation's own words) count as vocal-active rather than
/// noise-floor residue or incidental bleed from other stems.
/// </summary>
public static class VocalActivitySettings
{
    private const double WindowSeconds = 2.0;
    private const double HopSeconds = 1.0;
    private const double MinActiveDurationSeconds = 2.0;
    private const long TicksPerSecond = 10_000_000L;

    public static StemActivityDetectionSettings ForSampleRate(int sampleRate)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, "Sample rate must be positive.");
        }

        return new StemActivityDetectionSettings(
            WindowSamples: (int)Math.Round(sampleRate * WindowSeconds),
            HopSamples: (int)Math.Round(sampleRate * HopSeconds),
            OnThreshold: 0.35,
            OffThreshold: 0.2,
            MinActiveDurationTicks: (long)Math.Round(MinActiveDurationSeconds * TicksPerSecond));
    }
}
