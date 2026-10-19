using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.WholeSong;

/// <summary>Default <see cref="IMotifVariationCatalog"/>. See the interface for the full rationale.</summary>
public sealed class MotifVariationCatalog : IMotifVariationCatalog
{
    private readonly IMotifVariationPlanStore _store;

    public MotifVariationCatalog(IMotifVariationPlanStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<MotifVariationPlanRecord?> GetAsync(Guid songId, CancellationToken ct)
    {
        var path = _store.GetPath(songId);
        if (!File.Exists(path))
        {
            return null;
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<MotifVariationPlanRecord> SaveAsync(Guid songId, MotifVariationPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();

        var existing = await GetAsync(songId, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var record = new MotifVariationPlanRecord(new SongId(songId), plan, existing is null ? 0 : existing.Version + 1);
        await _store.SaveAsync(record, existing?.Version, ct).ConfigureAwait(false);
        return record;
    }
}
