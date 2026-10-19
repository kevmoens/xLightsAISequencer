using System.Text.Json;
using XlightsAI.Studio.Contracts;

namespace XlightsAI.Studio.Rhythm;

/// <summary>
/// JSON-file-backed <see cref="IEnergyEnvelopeStore"/>. Writes are atomic (same-directory temp file +
/// <see cref="File.Move(string, string, bool)"/> replace) — the same pattern
/// <c>JsonWaveformPyramidStore</c> already uses. Each envelope is written to its own file named
/// deterministically by its <see cref="EnergyEnvelope.SongId"/>, so re-running
/// <see cref="EnergyEnvelopeAnalyzer.AnalyzeAsync"/> for the same song always resolves to the same path.
/// </summary>
public sealed class JsonEnergyEnvelopeStore : IEnergyEnvelopeStore
{
    private readonly string _directory;

    public JsonEnergyEnvelopeStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    public string GetPath(SongId songId) =>
        Path.Combine(_directory, $"energy-envelope-{songId.Value:N}.json");

    public async Task<string> SaveAsync(EnergyEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(_directory);
        var finalPath = GetPath(envelope.SongId);
        var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";

        try
        {
            var json = JsonSerializer.Serialize(envelope);
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

    public async Task<EnergyEnvelope> LoadAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No energy envelope cache exists at '{path}'.", path);
        }

        await using var stream = File.OpenRead(path);
        try
        {
            var envelope = await JsonSerializer.DeserializeAsync<EnergyEnvelope>(stream, cancellationToken: ct).ConfigureAwait(false);
            return envelope ?? throw new EnergyEnvelopeCorruptException(path, new InvalidOperationException("Deserialized to null."));
        }
        catch (JsonException ex)
        {
            throw new EnergyEnvelopeCorruptException(path, ex);
        }
    }
}
