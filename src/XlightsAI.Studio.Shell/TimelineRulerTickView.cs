namespace XlightsAI.Studio.Shell;

/// <summary>
/// UI-only projection of <see cref="XlightsAI.Studio.Editing.TimelineRulerTick"/>: the domain
/// generator's absolute pixel <c>X</c> is converted to a left margin relative to the previous
/// tick, so the view can lay ticks out left-to-right in a plain horizontal panel without needing
/// Avalonia-specific absolute (Canvas) positioning inside this Avalonia-free project.
/// </summary>
public sealed record TimelineRulerTickView(string Label, double MarginLeft);
