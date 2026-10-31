namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 8 task 4: "Show-length guidance (10-15 minute target) surfaced in the Director's
/// planning." Architecture §10: "community guidance puts an optimal show at 10-15 minutes, with most
/// visitors watching 5-20, and xLights preview/memory degrading around 14-15 minutes." This pipeline
/// generates one song at a time (<see cref="ChoreographyGenerationPipeline"/>); there is no multi-song
/// show orchestrator in this codebase yet (<c>XlightsAI.Studio.Pipeline.WholeSongGenerator</c> plans one
/// song's own internal structure, not a sequence of songs), so true whole-show-length enforcement across
/// several songs is out of scope here -- this surfaces the target as explicit prose guidance for the
/// Director's own per-song pacing/density decisions, pending that future orchestration layer.
/// </summary>
public static class ShowLengthGuidance
{
    public const int TargetMinutesMin = 10;
    public const int TargetMinutesMax = 15;

    public const string PromptText =
        "A full show (across every song, if this is one of several) should land in the 10-15 minute band -- " +
        "most visitors watch 5-20 minutes and xLights preview/memory degrades past ~14-15 minutes. Pace this " +
        "song's own density and section count with that whole-show budget in mind.";
}
