namespace XlightsAI.Studio.Melody;

/// <summary>
/// One raw per-hop fundamental-frequency estimate -- Step 046's own "preserve raw contour for fallback"
/// architectural design note: a frame is emitted at every analyzed hop position regardless of whether
/// the source was judged voiced or unvoiced at that instant, so a later step's contour-direction
/// classification (Step 047) or low-pitch-confidence fallback (Step 051) always has a complete,
/// unbroken timeline to consume -- never a sparse "only the confident frames" list.
/// </summary>
/// <param name="TimeTicks">This frame's analysis-window start, in canonical 100 ns ticks from the source's own sample index 0.</param>
/// <param name="FrequencyHz">
/// Estimated fundamental frequency in Hz, sub-sample-refined (parabolic interpolation around the chosen
/// lag). <c>0</c> exactly for an unvoiced/silent frame -- never a fabricated best guess.
/// </param>
/// <param name="MidiNote">Nearest integer MIDI note number for <see cref="FrequencyHz"/> (never a raw Hz value in later contracts); <see langword="null"/> for an unvoiced frame.</param>
/// <param name="CentsOffset">Signed deviation from <see cref="MidiNote"/>'s exact pitch, in cents, in <c>[-50, 50]</c>; <see langword="null"/> for an unvoiced frame.</param>
/// <param name="Confidence">
/// Analyzer confidence this frame is genuinely voiced/pitched, in <c>[0, 1]</c> -- <c>0</c> exactly for
/// an unvoiced frame. Derived from how far below <see cref="PitchTrackingSettings.VoicingThreshold"/>
/// the chosen lag's own normalized difference value fell (lower difference = higher confidence).
/// </param>
public sealed record PitchFrame(
    long TimeTicks,
    double FrequencyHz,
    int? MidiNote,
    double? CentsOffset,
    double Confidence);
