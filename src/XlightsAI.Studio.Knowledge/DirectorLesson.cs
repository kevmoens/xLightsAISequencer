namespace XlightsAI.Studio.Knowledge;

/// <summary>
/// User feedback (2026-11-14): one piece of Director prompt guidance externalized out of
/// <c>XlightsAI.Studio.Director.DirectorPromptBuilder</c>'s own hardcoded C# string literals into a
/// plain Markdown file -- see <see cref="DirectorLessonCatalogLoader"/>'s own doc comment for why.
/// </summary>
/// <param name="Id">Stable identity, also the loose-override merge key (see <see cref="DirectorLessonCatalogLoader.Load(string?)"/>) -- the Markdown file's own name without extension.</param>
/// <param name="Applies">
/// A real, closed tag naming when this lesson is relevant -- evaluated against real facts already
/// known about THIS song's layout at prompt-build time (never a guess, never a general expression
/// language): <c>"always"</c>, <c>"tree-or-matrix-present"</c> (this layout has a real Tree/Matrix-class
/// model), or <c>"multi-model-group-present"</c> (this layout has a real group with 3+ individually
/// positioned members -- the exact precondition a <c>Look.Travel</c> sweep needs). An unrecognized tag
/// is treated as never-applicable rather than guessed into "always" -- see
/// <c>DirectorPromptBuilder.AppendApplicableLessons</c>.
/// </param>
/// <param name="Priority">Lower shows first/more prominently among lessons that apply to the same generation -- never a floor/validation concern, purely display ordering.</param>
/// <param name="Body">The real prompt text, verbatim -- appended as-is (no Markdown rendering; this is plain text wrapped in a Markdown file purely for human editability).</param>
public sealed record DirectorLesson(string Id, string Applies, int Priority, string Body)
{
    public const string AppliesAlways = "always";
    public const string AppliesTreeOrMatrixPresent = "tree-or-matrix-present";
    public const string AppliesMultiModelGroupPresent = "multi-model-group-present";
}

/// <summary>The full set of lessons a Director prompt may draw from -- seed (shipped) + any loose additions, see <see cref="DirectorLessonCatalogLoader"/>.</summary>
public sealed record DirectorLessonCatalog(IReadOnlyList<DirectorLesson> Lessons);
