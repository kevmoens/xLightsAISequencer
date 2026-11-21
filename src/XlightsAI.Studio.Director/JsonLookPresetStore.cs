using System.Text.Json;
using XlightsAI.Studio.Choreography;

namespace XlightsAI.Studio.Director;

/// <summary>
/// JSON-file-backed <see cref="ILookPresetStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace), the exact same pattern
/// <c>Composition.JsonVisualBudgetPresetStore</c> already uses. Assumes <see cref="LookPreset.Name"/> has
/// already been validated as filename-safe by <see cref="LookPresetCatalog"/> before reaching this store.
/// </summary>
public sealed class JsonLookPresetStore : ILookPresetStore
{
    private const string Prefix = "look-preset-";
    private const string Suffix = ".json";

    private readonly string _directory;

    public JsonLookPresetStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.Combine(_directory, $"{Prefix}{name}{Suffix}");
    }

    public async Task<string> SaveAsync(LookPreset preset, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(preset);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(preset.Name);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(preset);
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

    public async Task<LookPreset> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No look preset exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var preset = await JsonSerializer.DeserializeAsync<LookPreset>(stream, cancellationToken: ct).ConfigureAwait(false);
            return preset ?? throw new LookPresetCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new LookPresetCorruptException(path, ex);
        }
    }

    public Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var names = Directory.EnumerateFiles(_directory, $"{Prefix}*{Suffix}")
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .Select(f => f![Prefix.Length..^Suffix.Length])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(names);
    }
}
