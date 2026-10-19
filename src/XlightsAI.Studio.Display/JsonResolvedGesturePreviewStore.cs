using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XlightsAI.Studio.Display;

/// <summary>
/// JSON-file-backed <see cref="IResolvedGesturePreviewStore"/>. Writes are atomic (same-directory temp file
/// + <see cref="File.Move(string, string, bool)"/> replace), the same pattern
/// <see cref="JsonSpatialDisplayGraphStore"/> already uses. Never opens, reads for writing, or otherwise
/// touches any <c>xlights_rgbeffects.xml</c>/show-directory file -- only its own cache directory.
/// </summary>
public sealed class JsonResolvedGesturePreviewStore : IResolvedGesturePreviewStore
{
    private readonly string _directory;

    public JsonResolvedGesturePreviewStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(string sourceLayoutHash, Guid pathId, int pathVersion, Guid melodyTrackId, string boundaryPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundaryPolicy);

        var compositeKey = $"{sourceLayoutHash}|{pathId:N}|{pathVersion}|{melodyTrackId:N}|{boundaryPolicy}";
        var shortHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compositeKey)))[..16];
        return Path.Combine(_directory, $"resolved-gesture-preview-{shortHash}.json");
    }

    public async Task<string> SaveAsync(ResolvedGesturePreview preview, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(preview);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(preview.SourceLayoutHash, preview.PathId, preview.PathVersion, preview.MelodyTrackId, preview.BoundaryPolicy);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(preview);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public async Task<ResolvedGesturePreview> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No resolved gesture preview cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var preview = await JsonSerializer.DeserializeAsync<ResolvedGesturePreview>(stream, cancellationToken: ct).ConfigureAwait(false);
            return preview ?? throw new SpatialDisplayGraphCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SpatialDisplayGraphCorruptException(path, ex);
        }
    }
}
