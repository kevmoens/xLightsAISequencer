using System.Text.Json;

namespace XlightsAI.Studio.Melody;

/// <summary>
/// JSON-file-backed <see cref="IMelodyTrackRevisionStore"/>. Writes are atomic (same-directory temp
/// file + <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <see cref="JsonMelodyTrackStore"/> already uses, applied to the richer revision-state artifact.
/// Each state is written to its own file named deterministically by its
/// <see cref="MelodyTrackRevisionState.TrackId"/>.
/// </summary>
public sealed class JsonMelodyTrackRevisionStore : IMelodyTrackRevisionStore
{
    private readonly string _directory;

    public JsonMelodyTrackRevisionStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(Guid trackId) =>
        Path.Combine(_directory, $"melody-track-revision-{trackId:N}.json");

    public async Task<string> SaveAsync(MelodyTrackRevisionState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(state.TrackId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(state);
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

    public async Task<MelodyTrackRevisionState?> LoadAsync(Guid trackId, CancellationToken ct)
    {
        var path = GetPath(trackId);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var state = await JsonSerializer.DeserializeAsync<MelodyTrackRevisionState>(stream, cancellationToken: ct).ConfigureAwait(false);
            return state ?? throw new MelodyTrackRevisionStateCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new MelodyTrackRevisionStateCorruptException(path, ex);
        }
    }
}
