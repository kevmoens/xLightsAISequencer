using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="ISongDirectionCatalog"/>. See the interface for the full rationale.</summary>
public sealed class SongDirectionCatalog : ISongDirectionCatalog
{
    private readonly ISongDirectionPlanStore _store;

    public SongDirectionCatalog(ISongDirectionPlanStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<SongDirectionPlanRecord?> GetAsync(Guid songId, CancellationToken ct)
    {
        var path = _store.GetPath(songId);
        if (!File.Exists(path))
        {
            return null;
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<SongDirectionPlanRecord> SaveAsync(Guid songId, SongDirectionPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();

        var existing = await GetAsync(songId, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var record = new SongDirectionPlanRecord(new SongId(songId), plan, existing is null ? 0 : existing.Version + 1);
        await _store.SaveAsync(record, existing?.Version, ct).ConfigureAwait(false);
        return record;
    }
}
