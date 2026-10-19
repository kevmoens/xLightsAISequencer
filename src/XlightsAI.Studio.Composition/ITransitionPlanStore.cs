namespace XlightsAI.Studio.Composition;

/// <summary>
/// Persistence boundary for a saved <see cref="TransitionPlanRecord"/>. Mirrors <see cref="IVisualStoryStore"/>'s
/// own save/load contract, atomic-write guarantee and deterministic-path-by-key convention -- a sibling
/// aggregate at its own separate on-disk path, keyed by <see cref="TransitionPlanRecord.PhraseId"/>.
/// </summary>
public interface ITransitionPlanStore
{
    /// <summary>The deterministic on-disk path <paramref name="phraseId"/>'s own plan would be saved at, whether or not it has been saved yet.</summary>
    string GetPath(Guid phraseId);

    /// <summary>Persists <paramref name="record"/> atomically at <see cref="GetPath"/> and returns that path.</summary>
    Task<string> SaveAsync(TransitionPlanRecord record, CancellationToken ct);

    /// <summary>Reads back a previously saved record from <paramref name="path"/> for independent inspection.</summary>
    /// <exception cref="FileNotFoundException"><paramref name="path"/> does not exist.</exception>
    /// <exception cref="CompositionCorruptException"><paramref name="path"/> exists but is not a valid serialized <see cref="TransitionPlanRecord"/>.</exception>
    Task<TransitionPlanRecord> LoadAsync(string path, CancellationToken ct);
}
