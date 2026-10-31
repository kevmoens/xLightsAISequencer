using System.Text.Json;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Build-plan Phase 5 task 6: "Persist the Show Bible as a diffable artifact under the project's
/// bible/ directory using the existing atomic JSON-store pattern" (architecture §13's own storage
/// layout: <c>{project}/bible/ShowBible v1, v2, ... (diffable)</c>). Each save is a new, numbered,
/// never-overwritten version file -- a human reviewing the project directory can diff <c>v1</c> against
/// <c>v2</c> directly as plain JSON text, and nothing is ever lost to a silent overwrite. Mirrors the
/// same atomic temp-file + <see cref="File.Move(string, string, bool)"/> pattern every other JSON store
/// in this codebase uses.
/// </summary>
public sealed class JsonShowBibleStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonShowBibleStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid songId, int version) =>
        Path.Combine(_directory, $"show-bible-{songId:N}-v{version}.json");

    /// <summary>Saves <paramref name="bible"/> as the next version after the highest one already persisted for its own <see cref="ShowBible.SongId"/> (starting at 1) -- never overwrites an earlier version.</summary>
    public async Task<string> SaveNextVersionAsync(ShowBible bible, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bible);

        Directory.CreateDirectory(_directory);
        var nextVersion = (await ListVersionsAsync(bible.SongId, ct).ConfigureAwait(false)).DefaultIfEmpty(0).Max() + 1;

        var finalPath = GetPath(bible.SongId, nextVersion);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(bible, WriteOptions);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: false);
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

    public Task<IReadOnlyList<int>> ListVersionsAsync(Guid songId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_directory))
        {
            return Task.FromResult<IReadOnlyList<int>>([]);
        }

        var prefix = $"show-bible-{songId:N}-v";
        var versions = Directory.GetFiles(_directory, $"{prefix}*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name![prefix.Length..])
            .Select(suffix => int.TryParse(suffix, out var v) ? v : (int?)null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .OrderBy(v => v)
            .ToList();

        return Task.FromResult<IReadOnlyList<int>>(versions);
    }

    public async Task<ShowBible?> LoadLatestAsync(Guid songId, CancellationToken ct)
    {
        var versions = await ListVersionsAsync(songId, ct).ConfigureAwait(false);
        return versions.Count == 0 ? null : await LoadVersionAsync(songId, versions[^1], ct).ConfigureAwait(false);
    }

    public async Task<ShowBible> LoadVersionAsync(Guid songId, int version, CancellationToken ct)
    {
        var path = GetPath(songId, version);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No Show Bible v{version} exists for song '{songId:N}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var bible = await JsonSerializer.DeserializeAsync<ShowBible>(stream, cancellationToken: ct).ConfigureAwait(false);
            return bible ?? throw new ShowBibleCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new ShowBibleCorruptException(path, ex);
        }
    }
}
