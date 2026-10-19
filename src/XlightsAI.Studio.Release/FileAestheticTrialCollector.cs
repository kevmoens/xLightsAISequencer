using System.Text.Json;

namespace XlightsAI.Studio.Release;

/// <summary>
/// JSON-file-backed <see cref="IAestheticTrialCollector"/>/<see cref="IAestheticTrialReader"/>
/// (<c>aesthetic-trials.json</c> under the supplied directory), same atomic temp-then-rename write
/// pattern already established by <see cref="XlightsAI.Studio.Projects.RecentProjectsStore"/>. Unlike
/// that store, a corrupt file is never silently treated as empty (see
/// <see cref="AestheticTrialStoreCorruptException"/>) — a human rating is accepted data, not a
/// disposable cache (Section 4).
/// </summary>
public sealed class FileAestheticTrialCollector : IAestheticTrialCollector, IAestheticTrialReader
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileAestheticTrialCollector(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _filePath = Path.Combine(directory, "aesthetic-trials.json");
    }

    public async Task RecordAsync(AestheticRating rating, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rating);
        rating.Validate();
        ct.ThrowIfCancellationRequested();

        // Real, actually-observed timestamp provenance (Step 099's own user-visible outcome, verbatim:
        // "...with timestamp provenance") is this store's own responsibility, not a caller-supplied value
        // that may be stale, clock-skewed, or simply omitted (default(DateTimeOffset)).
        var stamped = rating with { RecordedUtc = DateTimeOffset.UtcNow };

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var existing = await LoadAllUnderGateAsync(ct).ConfigureAwait(false);
            var updated = new List<AestheticRating>(existing.Count + 1);
            updated.AddRange(existing);
            updated.Add(stamped);

            await WriteAtomicallyAsync(updated, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AestheticRating>> LoadAllAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await LoadAllUnderGateAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AestheticRating>> LoadAllUnderGateAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<AestheticRating>();
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AestheticTrialStoreCorruptException(_filePath, ex);
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<AestheticRating>>(json);
            return (IReadOnlyList<AestheticRating>?)entries ?? Array.Empty<AestheticRating>();
        }
        catch (JsonException ex)
        {
            throw new AestheticTrialStoreCorruptException(_filePath, ex);
        }
    }

    private async Task WriteAtomicallyAsync(IReadOnlyList<AestheticRating> entries, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Unique per call: two concurrent writers must never share a temp file. Written in full before
        // the rename, so a cancellation after this point either lands as a complete rename or leaves the
        // original file completely untouched -- never a half-written aesthetic-trials.json.
        var tempPath = $"{_filePath}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(entries), ct).ConfigureAwait(false);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
