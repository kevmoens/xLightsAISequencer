using Avalonia.Controls;
using XlightsAI.Studio.Shell;

namespace XlightsAI.Studio.App.Views.Screens;

public partial class TimelineView : UserControl
{
    public TimelineView() => InitializeComponent();

    /// <summary>
    /// Reports the ruler surface's real on-screen width to the view model.
    /// <para>
    /// <see cref="TimelineViewModel.ViewportWidthPixels"/> has always existed for this — its own
    /// documentation says it is updated "on every scroll/zoom/resize" by "the real view" — but no
    /// view ever set it, so the timeline silently kept its
    /// <see cref="TimelineViewModel.DefaultViewportWidthPixels"/> of 760 no matter how wide the
    /// window was. Tick generation, waveform bar layout, zoom-to-selection and the visible-window
    /// readouts all derive from this one number, so wiring it is what makes the timeline actually
    /// fill the window instead of drawing a fixed-width strip inside it.
    /// </para>
    /// </summary>
    private void OnTimelineSurfaceSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var width = e.NewSize.Width;

        // A zero/NaN width happens during teardown and before the first real layout pass; writing
        // it would collapse the viewport transform and divide-by-zero the zoom maths.
        if (double.IsFinite(width) && width > 0)
        {
            shell.Timeline.ViewportWidthPixels = width;
        }
    }
}
