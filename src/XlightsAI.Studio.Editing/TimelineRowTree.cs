namespace XlightsAI.Studio.Editing;

/// <summary>
/// PHASE-03 Step 017: the full row hierarchy built by <see cref="TimelineRowTreeBuilder"/> from one
/// imported <see cref="XlightsAI.Studio.Xlights.DisplayGraph"/>, plus any non-fatal findings
/// encountered while building it (e.g. a group-reference cycle) — same "never throw, always report"
/// discipline <see cref="XlightsAI.Studio.Xlights.IShowLayoutImporter"/> already uses for its own
/// <c>Warnings</c>, so a malformed/unusual layout still fails safely: every row stays visible, and
/// the caller can still surface what was wrong.
/// </summary>
public sealed record TimelineRowTree(IReadOnlyList<TimelineRowNode> Roots, IReadOnlyList<string> Warnings);
