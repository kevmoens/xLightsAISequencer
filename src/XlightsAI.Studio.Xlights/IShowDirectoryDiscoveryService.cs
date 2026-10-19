namespace XlightsAI.Studio.Xlights;

/// <summary>
/// PHASE-02 Step 009: read-only initial discovery of a candidate show directory. Only reads
/// files/metadata under the selected directory (resolved to its real, non-symlinked path);
/// never writes, moves, or deletes anything.
/// </summary>
public interface IShowDirectoryDiscoveryService
{
    /// <summary>
    /// Discovers which required/optional xLights show files are present under
    /// <paramref name="showDirectory"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="showDirectory"/> is null/blank.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="showDirectory"/> does not exist.</exception>
    Task<ShowDirectoryDiscovery> DiscoverAsync(string showDirectory, CancellationToken ct);
}
