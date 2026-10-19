namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-04 Step 027 ("Render synchronized waveform beneath ruler"): one pixel-space bar derived
/// from a single <see cref="XlightsAI.Studio.Audio.WaveformEnvelope"/>, mapped through the same
/// shared <see cref="TimelineViewport"/> pixel/tick transform <see cref="EffectBlockLayoutBuilder"/>
/// already uses — "Waveform position matches timeline coordinates" (this step's required acceptance).
/// </summary>
/// <param name="X">Absolute horizontal pixel position of this envelope's <c>StartSample</c>, via <see cref="TimelineViewport.XFromTime"/>.</param>
/// <param name="Width">Pixel width spanning this envelope's half-open sample range; never negative.</param>
/// <param name="Min">The source envelope's minimum sample value, unchanged.</param>
/// <param name="Max">The source envelope's maximum sample value, unchanged.</param>
/// <param name="Rms">The source envelope's RMS sample value, unchanged.</param>
public sealed record WaveformBarLayout(double X, double Width, float Min, float Max, float Rms);
