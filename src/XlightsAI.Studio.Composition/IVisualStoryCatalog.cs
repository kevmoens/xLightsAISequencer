namespace XlightsAI.Studio.Composition;

/// <summary>
/// PHASE-10 Step 069's own application-orchestration boundary: validates, persists and versions one song's
/// <see cref="VisualStory"/>. Rejects an invalid story (<see cref="VisualStory.Validate"/> failure)
/// <em>before</em> loading or writing anything -- this step's own required failure/edge case, verbatim:
/// "Invalid input, cancellation or stale persisted state must not corrupt the project."
/// </summary>
public interface IVisualStoryCatalog
{
    /// <summary>Loads a previously saved story for <paramref name="songId"/>, or <see langword="null"/> if none has ever been saved.</summary>
    Task<VisualStoryRecord?> GetAsync(Guid songId, CancellationToken ct);

    /// <summary>
    /// Saves <paramref name="story"/> under <paramref name="songId"/>, replacing any prior story for the same
    /// song and incrementing its <see cref="VisualStoryRecord.Version"/>.
    /// </summary>
    /// <exception cref="CompositionInvalidException"><paramref name="story"/> fails <see cref="VisualStory.Validate"/>.</exception>
    Task<VisualStoryRecord> SaveAsync(Guid songId, VisualStory story, CancellationToken ct);
}
