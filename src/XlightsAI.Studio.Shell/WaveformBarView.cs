namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of <see cref="XlightsAI.Studio.Editing.WaveformBarLayout"/>: the domain
/// builder's absolute pixel <c>X</c> is converted to a left margin relative to the previous bar
/// (same convention as <see cref="TimelineRulerTickView"/>/<see cref="EffectBlockItemView"/>), so
/// the view lays bars out left-to-right in a plain horizontal panel without needing Avalonia-specific
/// absolute (Canvas) positioning inside this Avalonia-free project.
/// </summary>
/// <param name="MarginLeft">Pixel gap between this bar's left edge and the previous bar's right edge.</param>
/// <param name="Width">This bar's pixel width.</param>
/// <param name="Min">The source envelope's minimum sample value, unchanged — lets the view scale a below-center extent.</param>
/// <param name="Max">The source envelope's maximum sample value, unchanged — lets the view scale an above-center extent.</param>
/// <param name="Rms">The source envelope's RMS sample value, unchanged.</param>
public sealed record WaveformBarView(double MarginLeft, double Width, float Min, float Max, float Rms)
{
    /// <summary>
    /// A simple, bounded vertical pixel height derived from this bar's peak amplitude
    /// (<c>Max - Min</c>), for a plain centered <c>Rectangle</c> to bind to directly. Never zero even
    /// for true silence (<c>Min = Max = 0</c>) — a thin, visible flat line, not an invisible gap, is
    /// this step's own "silence... must be handled explicitly" requirement applied to the view.
    /// </summary>
    public double BarHeight => Math.Clamp((Max - Min) * 20.0, 1.0, 40.0);
}
