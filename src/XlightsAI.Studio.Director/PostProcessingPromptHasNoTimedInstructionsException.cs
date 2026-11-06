namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" post-processing phase (2026-11-02): thrown when
/// <see cref="ChoreographyGenerationPipeline.ApplyPostProcessingPromptAsync"/>'s own prompt text carries no
/// <c>mm:ss</c>/<c>hh:mm:ss</c> timestamp token at all -- a post-processing prompt is, by definition, a set
/// of time-ranged touch-ups; free text with no timestamp has nothing for this entry point to act on
/// (ordinary free-text creative direction belongs in the pre-generation custom prompt instead).
/// </summary>
public sealed class PostProcessingPromptHasNoTimedInstructionsException : Exception
{
    public PostProcessingPromptHasNoTimedInstructionsException()
        : base("No time range (e.g. \"00:24-00:25\") was found in this touch-up prompt. A post-processing prompt needs at least one timestamp naming when to apply the change.")
    {
    }
}
