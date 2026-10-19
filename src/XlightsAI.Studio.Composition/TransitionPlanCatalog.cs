namespace XlightsAI.Studio.Composition;

/// <summary>Default <see cref="ITransitionPlanCatalog"/>. See the interface and <c>implementation-evidence/PHASE-10/STEP-070.md</c> for the full rationale.</summary>
public sealed class TransitionPlanCatalog : ITransitionPlanCatalog
{
    private readonly ITransitionPlanStore _store;

    public TransitionPlanCatalog(ITransitionPlanStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async Task<TransitionPlanRecord?> GetAsync(Guid phraseId, CancellationToken ct)
    {
        var path = _store.GetPath(phraseId);
        if (!File.Exists(path))
        {
            return null;
        }

        return await _store.LoadAsync(path, ct).ConfigureAwait(false);
    }

    public async Task<TransitionPlanRecord> SaveAsync(Guid phraseId, TransitionPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();

        var existing = await GetAsync(phraseId, ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var record = new TransitionPlanRecord(phraseId, plan, existing is null ? 0 : existing.Version + 1);
        await _store.SaveAsync(record, ct).ConfigureAwait(false);
        return record;
    }
}
