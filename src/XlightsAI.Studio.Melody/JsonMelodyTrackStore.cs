using System.Text.Json;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// JSON-file-backed <see cref="IMelodyTrackStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) -- the same pattern
/// <c>XlightsAI.Studio.Stems.JsonStemActivityResultStore</c> already uses. Each track is written to its
/// own file named deterministically by its <see cref="MelodyTrack.Id"/>. Only ever persists a track that
/// has already passed <see cref="MelodyTrackValidator.Validate"/> -- <see cref="SaveAsync"/> re-validates
/// defensively so an invalid track is never written to disk regardless of caller discipline.
/// </summary>
public sealed class JsonMelodyTrackStore : IMelodyTrackStore
{
    private readonly string _directory;

    public JsonMelodyTrackStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid trackId) =>
        Path.Combine(_directory, $"melody-track-{trackId:N}.json");

    public async Task<string> SaveAsync(MelodyTrack track, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(track);
        var validated = MelodyTrackValidator.Validate(track);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(validated.Id);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(validated);
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

    public async Task<MelodyTrack> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No melody track exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var track = await JsonSerializer.DeserializeAsync<MelodyTrack>(stream, cancellationToken: ct).ConfigureAwait(false);
            return track ?? throw new MelodyTrackCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new MelodyTrackCorruptException(path, ex);
        }
    }
}
