using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XlightsAI.Studio.Display;

/// <summary>
/// JSON-file-backed <see cref="ILockedNodeStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <see cref="JsonRoleAssignmentStore"/>/<see cref="JsonSpatialPathStore"/> already use. Never opens, reads
/// for writing, or otherwise touches any <c>xlights_rgbeffects.xml</c>/show-directory file or any sibling
/// cache file -- only its own cache directory.
/// </summary>
public sealed class JsonLockedNodeStore : ILockedNodeStore
{
    private readonly string _directory;

    public JsonLockedNodeStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(string sourceLayoutHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLayoutHash);
        var shortHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceLayoutHash)))[..16];
        return Path.Combine(_directory, $"locked-nodes-{shortHash}.json");
    }

    public async Task<string> SaveAsync(LockedNodeSet locked, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(locked);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(locked.SourceLayoutHash);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(locked);
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

    public async Task<LockedNodeSet> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No locked node set exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var locked = await JsonSerializer.DeserializeAsync<LockedNodeSet>(stream, cancellationToken: ct).ConfigureAwait(false);
            return locked ?? throw new SpatialDisplayGraphCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new SpatialDisplayGraphCorruptException(path, ex);
        }
    }
}
