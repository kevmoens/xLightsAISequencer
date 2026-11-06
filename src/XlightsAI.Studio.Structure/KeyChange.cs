namespace XlightsAI.Studio.Structure;

/// <summary>
/// Build-plan "richer analysis tools" Phase 7 (2026-11-02): one real, measured moment this song's
/// detected key/mode changed, produced by <see cref="KeyDetector"/> from <see cref="BarFingerprintBuilder"/>'s
/// existing per-bar chroma histograms -- no new chroma extraction, just a key-profile correlation over
/// data this codebase already computes.
/// </summary>
/// <param name="TimeTicks">The instant (100ns ticks from song start) this key change is detected at -- the first bar's own start where the new key became stable.</param>
/// <param name="KeyName">The detected key/mode, e.g. <c>"C Major"</c>/<c>"A Minor"</c>.</param>
/// <param name="Confidence">The winning key profile's own Pearson correlation against this bar's chroma vector, roughly <c>[-1,1]</c> in practice mostly positive -- a disclosed heuristic strength, not a calibrated probability.</param>
public sealed record KeyChange(long TimeTicks, string KeyName, double Confidence);
