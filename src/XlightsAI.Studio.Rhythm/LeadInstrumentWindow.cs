namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// Build-plan Phase 27 (2026-11-14): one real, sustained span where a real lead instrument/section
/// (horns, strings, a riff -- anything in the Mid/High bands) is genuinely prominent relative to its own
/// recent surroundings, as found by <see cref="LeadInstrumentWindowDetector"/>.
/// </summary>
/// <param name="StartTicks">Start of the window (100ns ticks), inclusive.</param>
/// <param name="EndTicks">End of the window (100ns ticks), exclusive.</param>
/// <param name="RelativeProminence">How far above its own trailing baseline this window's Mid+High energy ran, averaged across the window (e.g. <c>2.0</c> means "twice the recent baseline") -- not an absolute loudness value, a real, relative, song-local measurement.</param>
public sealed record LeadInstrumentWindow(long StartTicks, long EndTicks, double RelativeProminence);
