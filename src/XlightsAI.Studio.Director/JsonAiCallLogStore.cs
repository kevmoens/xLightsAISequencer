using System.Text.Json;

namespace XlightsAI.Studio.Director;

/// <summary>
/// Architecture §13's own storage layout: <c>{project}/llm/</c>, "full call log with costs." One file per
/// call (never one growing append-only log file) -- same atomic temp-file + <see cref="File.Move(string, string, bool)"/>
/// pattern every other JSON store in this codebase uses (mirrors <see cref="JsonShowBibleStore"/>), chosen
/// over a single NDJSON file specifically because <see cref="SectionScriptBatchGenerator"/> appends many
/// entries concurrently across a batch of section calls -- one file per call needs no cross-call write
/// lock, where a single shared log file would.
/// </summary>
public sealed class JsonAiCallLogStore
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private readonly string _directory;

    public JsonAiCallLogStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <summary>Appends <paramref name="entry"/> as its own new file -- never overwrites an existing entry.</summary>
    public async Task AppendAsync(AiCallLogEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(_directory);
        var finalPath = Path.Combine(_directory, $"call-{entry.TimestampUtc.UtcTicks}-{Guid.NewGuid():N}.json");
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(entry, WriteOptions);
            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, finalPath, overwrite: false);
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

    public async Task<IReadOnlyList<AiCallLogEntry>> ListForSongAsync(Guid songId, CancellationToken ct)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var entries = new List<AiCallLogEntry>();
        foreach (var file in Directory.GetFiles(_directory, "call-*.json"))
        {
            ct.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(file);
            AiCallLogEntry? entry;
            try
            {
                entry = await JsonSerializer.DeserializeAsync<AiCallLogEntry>(stream, cancellationToken: ct).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                continue; // A corrupt individual call-log entry never blocks reading the rest.
            }

            if (entry is not null && entry.SongId == songId)
            {
                entries.Add(entry);
            }
        }

        return entries.OrderBy(e => e.TimestampUtc).ToList();
    }

    /// <summary>Sums every known (non-null) <see cref="AiCallLogEntry.EstimatedCostUsd"/> logged for <paramref name="songId"/> -- a generation that used only unrecognized-pricing models (e.g. a free local Ollama run) legitimately sums to <c>0</c>, which this returns distinctly from "no calls at all" only via <see cref="ListForSongAsync"/>.</summary>
    public async Task<decimal> SumCostForSongAsync(Guid songId, CancellationToken ct)
    {
        var entries = await ListForSongAsync(songId, ct).ConfigureAwait(false);
        return entries.Sum(e => e.EstimatedCostUsd ?? 0m);
    }
}
