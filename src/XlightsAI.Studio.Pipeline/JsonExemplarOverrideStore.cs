using System.Text.Json;

namespace XlightsAI.Studio.Pipeline;

/// <summary>
/// Build-plan Phase 17 §2e: JSON-file-backed persistence for the per-exemplar enabled/disabled map (see
/// <see cref="ExemplarOverrides"/>), deliberately a separate file from <c>exemplar-index.json</c> --
/// same "overrides survive a re-mine" convention as <c>JsonSectionLabelOverrideStore</c>/
/// <c>JsonPhraseBoundaryOverrideStore</c> (<c>XlightsAI.Studio.Structure</c>): an automatic re-mine can
/// freely rebuild <c>exemplar-index.json</c> from scratch without ever touching or losing a user's
/// curation choices here. One file per corpus knowledge directory (matching <see cref="JsonExemplarIndexStore"/>'s
/// own single-file-per-root convention, not a per-song file -- the exemplar index itself is global, not
/// per-song). A missing file is a valid, empty override set (every exemplar enabled by default), never an
/// error -- unlike <see cref="JsonExemplarIndexStore.LoadAsync"/>, which treats a missing required
/// artifact as a real failure; this file is optional by design.
/// </summary>
public sealed class JsonExemplarOverrideStore
{
    private const string FileName = "exemplar-overrides.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonExemplarOverrideStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    /// <summary>Missing file returns an empty, mutable-by-caller map -- never throws.</summary>
    public async Task<IReadOnlyDictionary<string, bool>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(Path))
        {
            return new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = File.OpenRead(Path);
            var overrides = await JsonSerializer.DeserializeAsync<Dictionary<string, bool>>(stream, cancellationToken: ct).ConfigureAwait(false);
            return overrides is null
                ? throw new ExemplarOverridesCorruptException(Path, new InvalidOperationException("Deserialized to null."))
                : overrides;
        }
        catch (JsonException ex)
        {
            throw new ExemplarOverridesCorruptException(Path, ex);
        }
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, bool> overrides, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = Path;
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(overrides, JsonOptions), ct).ConfigureAwait(false);
            File.Move(tempPath, finalPath, overwrite: true);
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
}

/// <summary>Thrown when <c>exemplar-overrides.json</c> exists but cannot be parsed as a curation map.</summary>
public sealed class ExemplarOverridesCorruptException(string path, Exception inner)
    : Exception($"Exemplar overrides file at '{path}' is corrupt and could not be parsed.", inner);
