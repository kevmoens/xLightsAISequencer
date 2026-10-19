namespace XlightsAI.Studio.Xlights;

/// <summary>
/// No-op <see cref="IShowLayoutImporter"/>, the same compatibility pattern
/// <see cref="NullShowDirectoryDiscoveryService"/> uses: lets <c>ShellViewModel</c> take an optional
/// layout-importer collaborator without breaking earlier steps' existing constructor call sites or
/// tests. Real composition (the App project) always supplies the real <see cref="ShowLayoutImporter"/>.
/// </summary>
public sealed class NullShowLayoutImporter : IShowLayoutImporter
{
    public static readonly NullShowLayoutImporter Instance = new();

    private NullShowLayoutImporter()
    {
    }

    public Task<DisplayGraph> ImportReadOnlyAsync(string showDirectory, CancellationToken ct) =>
        throw new NotSupportedException("No show layout importer is configured.");
}
