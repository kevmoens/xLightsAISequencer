namespace XlightsAI.Studio.Xlights;

/// <summary>
/// No-op <see cref="ISequenceDocumentStore"/>, the same compatibility pattern
/// <see cref="NullShowDirectoryDiscoveryService"/>/<c>NullProjectService</c> use: lets
/// <c>ShellViewModel</c> take an optional store collaborator without breaking earlier steps'
/// existing constructor call sites or tests. Unlike <see cref="ShowLayoutImporter"/>/
/// <see cref="ThreeEffectPlanFactory"/> (pure, stateless domain logic with nothing to configure),
/// this store needs an external output directory before it can do anything meaningful, so it follows
/// the throwing-stub pattern rather than defaulting to a real instance. Real composition (the App
/// project) always supplies a real, directory-configured <see cref="JsonSequenceDocumentStore"/>.
/// </summary>
public sealed class NullSequenceDocumentStore : ISequenceDocumentStore
{
    public static readonly NullSequenceDocumentStore Instance = new();

    private NullSequenceDocumentStore()
    {
    }

    public Task<string> SaveAsync(SequenceDocument document, CancellationToken ct) =>
        throw new NotSupportedException("No sequence document store is configured.");

    public Task<SequenceDocument> LoadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException("No sequence document store is configured.");
}
