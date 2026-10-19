using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Audio;

/// <summary>
/// No-op <see cref="IWaveformPyramidStore"/>, the same compatibility pattern <see cref="NullSongAssetStore"/>
/// uses: lets a view model/builder take an optional store collaborator without breaking earlier call
/// sites or tests. A pyramid store needs a real configured output directory before it can do
/// anything meaningful, so it follows the throwing-stub pattern rather than defaulting to a real
/// instance.
/// </summary>
public sealed class NullWaveformPyramidStore : IWaveformPyramidStore
{
    public static readonly NullWaveformPyramidStore Instance = new();

    private NullWaveformPyramidStore()
    {
    }

    public string GetPath(SongId songId) =>
        throw new NotSupportedException("No waveform pyramid store is configured.");

    public Task<string> SaveAsync(WaveformPyramidCache cache, CancellationToken ct) =>
        throw new NotSupportedException("No waveform pyramid store is configured.");

    public Task<WaveformPyramidCache> LoadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException("No waveform pyramid store is configured.");
}
