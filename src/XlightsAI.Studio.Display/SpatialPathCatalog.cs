using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Display;

/// <summary>Default <see cref="ISpatialPathCatalog"/>. See the interface and <c>implementation-evidence/PHASE-09/STEP-062.md</c> for the full rationale.</summary>
public sealed class SpatialPathCatalog : ISpatialPathCatalog
{
    private readonly ISpatialPathStore _store;
    private readonly ISpatialPathSuggester _suggester;
    private readonly ISpatialPathEditor _editor;

    public SpatialPathCatalog(ISpatialPathStore store, ISpatialPathSuggester suggester, ISpatialPathEditor editor)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(suggester);
        ArgumentNullException.ThrowIfNull(editor);
        _store = store;
        _suggester = suggester;
        _editor = editor;
    }

    public async Task<SpatialPathSet> GetPathsAsync(string sourceLayoutHash, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);

        var path = _store.GetPath(sourceLayoutHash);
        if (!File.Exists(path))
        {
            return SpatialPathSet.Empty(sourceLayoutHash);
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<SpatialPathSet> CreateAsync(SpatialDisplayGraph graph, string name, IReadOnlyList<ModelId> candidateNodeIds,
        string directionPolicy, bool isClosed, bool allowReplaceExisting, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        // Validates candidateNodeIds/directionPolicy and builds the brand-new ordering before anything
        // is loaded or written -- this step's own required failure/edge case, verbatim.
        var suggested = _suggester.Suggest(graph, name, candidateNodeIds, directionPolicy, isClosed);

        var current = await GetPathsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        var existing = current.Paths.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

        if (existing is not null && !allowReplaceExisting)
        {
            throw new SpatialDisplayGraphInvalidException(
                $"A path named '{name}' already exists for this layout; pass allowReplaceExisting to update it.");
        }

        ct.ThrowIfCancellationRequested();

        List<SpatialPath> newPaths;
        List<SpatialPath> newHistory;
        if (existing is null)
        {
            newPaths = [.. current.Paths, suggested];
            newHistory = [.. current.History];
        }
        else
        {
            var replacement = suggested with { Id = existing.Id, Version = existing.Version + 1 };
            newPaths = current.Paths.Select(p => p.Id == existing.Id ? replacement : p).ToList();
            newHistory = [.. current.History, existing];
        }

        var updated = current with { Paths = newPaths, History = newHistory, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }

    public Task<SpatialPathSet> ReorderAsync(SpatialDisplayGraph graph, Guid pathId, IReadOnlyList<ModelId> newOrder, CancellationToken ct) =>
        ApplyAsync(graph, pathId, path => _editor.Reorder(path, newOrder), ct);

    public Task<SpatialPathSet> ReverseAsync(SpatialDisplayGraph graph, Guid pathId, CancellationToken ct) =>
        ApplyAsync(graph, pathId, _editor.Reverse, ct);

    private async Task<SpatialPathSet> ApplyAsync(SpatialDisplayGraph graph, Guid pathId, Func<SpatialPath, SpatialPath> apply, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var current = await GetPathsAsync(graph.SourceLayoutHash, ct).ConfigureAwait(false);
        var existing = current.Paths.FirstOrDefault(p => p.Id == pathId) ??
            throw new SpatialDisplayGraphInvalidException($"No path with id '{pathId}' exists for this layout.");

        // May itself throw SpatialDisplayGraphInvalidException (e.g. an invalid reorder set) before any
        // write is attempted -- the exact same "validate before persist" convention IDisplayLayoutEditor
        // already follows.
        var replacement = apply(existing);

        ct.ThrowIfCancellationRequested();

        var newPaths = current.Paths.Select(p => p.Id == existing.Id ? replacement : p).ToList();
        var newHistory = new List<SpatialPath>(current.History) { existing };
        var updated = current with { Paths = newPaths, History = newHistory, Version = current.Version + 1 };
        await _store.SaveAsync(updated, ct).ConfigureAwait(false);
        return updated;
    }
}
