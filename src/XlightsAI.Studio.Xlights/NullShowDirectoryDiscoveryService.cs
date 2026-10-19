namespace XlightsAI.Studio.Xlights;

/// <summary>
/// No-op <see cref="IShowDirectoryDiscoveryService"/>, the same compatibility pattern
/// <c>NullProjectService</c> uses: lets <c>ShellViewModel</c> take an optional discovery-service
/// collaborator without breaking earlier steps' existing constructor call sites or tests. Real
/// composition (the App project) always supplies the real <see cref="ShowDirectoryDiscoveryService"/>.
/// </summary>
public sealed class NullShowDirectoryDiscoveryService : IShowDirectoryDiscoveryService
{
    public static readonly NullShowDirectoryDiscoveryService Instance = new();

    private NullShowDirectoryDiscoveryService()
    {
    }

    public Task<ShowDirectoryDiscovery> DiscoverAsync(string showDirectory, CancellationToken ct) =>
        throw new NotSupportedException("No show directory discovery service is configured.");
}
