namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="IVisualStoryCatalog"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-069.md</c> for the full rationale.</summary>
public sealed class VisualStoryCatalog : IVisualStoryCatalog
{
    private readonly IVisualStoryStore _store;

    public VisualStoryCatalog(IVisualStoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<VisualStoryRecord?> GetAsync(Guid songId, CancellationToken ct)
    {
        var path = _store.GetPath(songId);
        if (!File.Exists(path))
        {
            return null;
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<VisualStoryRecord> SaveAsync(Guid songId, VisualStory story, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(story);
        story.Validate();

        var existing = await GetAsync(songId, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var record = new VisualStoryRecord(songId, story, existing is null ? 0 : existing.Version + 1);
        await _store.SaveAsync(record, ct).ConfigureAwait(false);
        return record;
    }
}
