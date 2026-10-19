namespace XlightsAI.Studio.Audio;

/// <summary>
/// No-op <see cref="ISongAssetStore"/>, the same compatibility pattern <c>NullSequenceDocumentStore</c>
/// uses: lets a view model take an optional store collaborator without breaking earlier call sites
/// or tests. A song-asset store needs a real configured output directory before it can do anything
/// meaningful, so it follows the throwing-stub pattern rather than defaulting to a real instance.
/// </summary>
public sealed class NullSongAssetStore : ISongAssetStore
{
    public static readonly NullSongAssetStore Instance = new();

    private NullSongAssetStore()
    {
    }

    public Task<string> SaveAsync(SongAsset asset, CancellationToken ct) =>
        throw new NotSupportedException("No song asset store is configured.");

    public Task<SongAsset> LoadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException("No song asset store is configured.");
}
