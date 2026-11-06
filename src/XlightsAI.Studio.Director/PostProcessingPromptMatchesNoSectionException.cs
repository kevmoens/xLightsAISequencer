namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan "richer analysis tools" post-processing phase (2026-11-02): thrown when
/// <see cref="ChoreographyGenerationPipeline.ApplyPostProcessingPromptAsync"/>'s own parsed time range(s)
/// overlap no real section's own window at all (e.g. a timestamp past the end of the song, or a typo) --
/// never silently a no-op, the same "disclosed, actionable, never a silent no-op" convention as
/// <see cref="QualityPassUnavailableException"/>.
/// </summary>
public sealed class PostProcessingPromptMatchesNoSectionException : Exception
{
    public PostProcessingPromptMatchesNoSectionException()
        : base("The time range(s) in this touch-up prompt do not overlap any section of the song (check for a typo, or a timestamp past the end of the track). Nothing was changed.")
    {
    }
}
